using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Events;
using Platform.Messaging.Telemetry;
using Platform.Messaging.Topology;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace Platform.Messaging.Consuming;

public sealed class ConsumerOptions
{
    public required string QueueName { get; init; }

    /// <summary>Максимум повторних спроб для тимчасових збоїв; після нього — Dead Letter Queue.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Скільки непідтверджених повідомлень споживач тримає одночасно.</summary>
    public ushort PrefetchCount { get; init; } = 10;

    /// <summary>Базова пауза перед повтором (множиться на номер спроби).</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Довготривалий споживач черги (BackgroundService) з ручним підтвердженням:
/// успіх → Ack; тимчасовий збій → повторна публікація зі збільшеним x-retry-count + Ack оригіналу;
/// постійний збій або вичерпані спроби → Nack(requeue: false) → Dead Letter Exchange → DLQ.
/// Повідомлення, повернене через requeue, прийшло б без змін (заголовок змінити не можна), тому лічильник спроб
/// ведеться повторною публікацією. Повтор іде в ТУ САМУ чергу (default exchange, routing key = ім'я черги),
/// а не в catalog-events — інакше копії отримали б усі сервіси-споживачі.
/// </summary>
public sealed class RabbitMqEventConsumer(
    ConsumerOptions options,
    EventHandlerRegistry registry,
    IConnection connection,
    IServiceScopeFactory scopes,
    MessagingMetrics metrics,
    ILogger<RabbitMqEventConsumer> logger) : BackgroundService
{
    private IChannel? _channel;
    private CancellationToken _stopping;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;

        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await MessagingTopology.DeclareAsync(_channel, stoppingToken);

        // BasicQos: не більше PrefetchCount непідтверджених повідомлень одночасно
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: options.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageAsync;

        // autoAck: false — підтвердження вручну після обробки
        await _channel.BasicConsumeAsync(options.QueueName, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Consumer started on queue {Queue} (prefetch {Prefetch}, max retries {MaxRetries})",
            options.QueueName, options.PrefetchCount, options.MaxRetries);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { /* коректна зупинка */ }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);   // непідтверджені повідомлення повернуться в чергу
            await _channel.DisposeAsync();
        }
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        var started = Stopwatch.GetTimestamp();
        var eventType = ea.BasicProperties.Type ?? "unknown";
        var messageId = ea.BasicProperties.MessageId;
        var correlationId = ReadHeader(ea, "x-correlation-id");

        // Контекст трасування з заголовків → Activity типу Consumer (дочірній до span публікації)
        using var activity = TraceContextPropagation.StartConsumerActivity(ea, options.QueueName, eventType);
        using var _1 = LogContext.PushProperty("EventId", messageId);
        using var _2 = LogContext.PushProperty("EventType", eventType);
        using var _3 = LogContext.PushProperty("CorrelationId", correlationId);
        using var _4 = LogContext.PushProperty("Queue", options.QueueName);
        Platform.ServiceDefaults.Correlation.CorrelationContext.Current = correlationId;

        var result = "processed";
        try
        {
            if (!registry.TryGet(eventType, out var entry))
            {
                // Подія, на яку цей сервіс не підписаний: не помилка — підтверджуємо й ігноруємо
                result = "ignored";
                logger.LogWarning("No handler for event type {EventType} (message {MessageId}); acknowledged and ignored", eventType, messageId);
                await AckAsync(ea);
                return;
            }

            var @event = (IntegrationEvent?)JsonSerializer.Deserialize(ea.Body.Span, entry.EventType, JsonDefaults.Options)
                         ?? throw new JsonException("Empty message body.");

            if (@event.SchemaVersion > IntegrationEvent.CurrentSchemaVersion)
                throw new PermanentFailureException(
                    $"Unsupported schema version {@event.SchemaVersion} (supported up to {IntegrationEvent.CurrentSchemaVersion}).");

            logger.LogInformation("Received {EventType} {EventId} from queue {Queue} (CorrelationId {CorrelationId}, redelivered {Redelivered})",
                eventType, @event.EventId, options.QueueName, correlationId, ea.Redelivered);

            // Scope на повідомлення: scoped-залежності (UnitOfWork, репозиторії) живуть рівно одну обробку
            await using var scope = scopes.CreateAsyncScope();
            var handling = await entry.Invoke(scope.ServiceProvider, @event, _stopping);
            result = handling == HandlingResult.Duplicate ? "duplicate" : handling == HandlingResult.Skipped ? "skipped" : "processed";

            await AckAsync(ea);
            logger.LogInformation("Acked {EventType} {EventId}: {Result}", eventType, @event.EventId, result);
        }
        catch (Exception ex) when (ex is JsonException or PermanentFailureException or NotSupportedException)
        {
            // Постійний збій: пошкоджені дані / несумісна версія — повтор не допоможе
            result = "dead_lettered";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Permanent failure for {EventType} message {MessageId}: sending to DLQ without retry", eventType, messageId);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, _stopping);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // Зупинка застосунку: не підтверджуємо — після закриття каналу повідомлення повернеться в чергу
            result = "cancelled";
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            result = await RetryOrDeadLetterAsync(ea, eventType, ex);
        }
        finally
        {
            metrics.Processed(options.QueueName, eventType, result, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    // Тимчасовий збій (БД недоступна, таймаут): повтор із лічильником x-retry-count; вичерпано спроби → DLQ
    private async Task<string> RetryOrDeadLetterAsync(BasicDeliverEventArgs ea, string eventType, Exception ex)
    {
        var attempt = ReadRetryCount(ea);

        if (attempt >= options.MaxRetries)
        {
            logger.LogError(ex, "Message {MessageId} ({EventType}) failed after {Attempts} attempts, sending to DLQ",
                ea.BasicProperties.MessageId, eventType, attempt);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, _stopping);   // → DLX → DLQ
            return "dead_lettered";
        }

        logger.LogWarning(ex, "Message {MessageId} ({EventType}) failed, retry {Attempt}/{Max} in {Delay} ms",
            ea.BasicProperties.MessageId, eventType, attempt + 1, options.MaxRetries,
            (options.RetryDelay * (attempt + 1)).TotalMilliseconds);

        try
        {
            await Task.Delay(options.RetryDelay * (attempt + 1), _stopping);   // проста лінійна затримка між спробами

            var headers = new Dictionary<string, object?>(ea.BasicProperties.Headers ?? new Dictionary<string, object?>())
            {
                ["x-retry-count"] = attempt + 1
            };
            var props = new BasicProperties(ea.BasicProperties) { Headers = headers };

            // Спершу копія зі збільшеним лічильником у ТУ САМУ чергу, потім Ack оригіналу
            await _channel!.BasicPublishAsync(exchange: "", routingKey: options.QueueName, mandatory: false, props, ea.Body, _stopping);
            await AckAsync(ea);
            return "retried";
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            return "cancelled";   // оригінал не підтверджено — повернеться в чергу
        }
    }

    private Task AckAsync(BasicDeliverEventArgs ea) =>
        _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, _stopping).AsTask();

    private static int ReadRetryCount(BasicDeliverEventArgs ea) =>
        ea.BasicProperties.Headers is { } h && h.TryGetValue("x-retry-count", out var v) && v is not null
            ? Convert.ToInt32(v)
            : 0;

    private static string? ReadHeader(BasicDeliverEventArgs ea, string key) =>
        ea.BasicProperties.Headers is { } h && h.TryGetValue(key, out var v)
            ? v switch { byte[] b => System.Text.Encoding.UTF8.GetString(b), string s => s, _ => v?.ToString() }
            : null;
}
