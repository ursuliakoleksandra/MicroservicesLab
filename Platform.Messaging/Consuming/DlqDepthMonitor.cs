using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Telemetry;
using Platform.Messaging.Topology;
using RabbitMQ.Client;

namespace Platform.Messaging.Consuming;

/// <summary>
/// Моніторинг глибини Dead Letter Queue: періодично читає кількість повідомлень у DLQ черги споживача й публікує
/// її як gauge-метрику messaging.dlq.depth. Зростання глибини = повідомлення, що потребують ручного розслідування.
/// </summary>
public sealed class DlqDepthMonitor(string queue, IConnection connection, MessagingMetrics metrics, ILogger<DlqDepthMonitor> logger)
    : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dlq = MessagingTopology.DeadLetterQueue(queue);
        using var timer = new PeriodicTimer(Period);
        do
        {
            try
            {
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                var info = await channel.QueueDeclarePassiveAsync(dlq, stoppingToken);
                metrics.SetDlqDepth(queue, info.MessageCount);
                if (info.MessageCount > 0)
                    logger.LogWarning("Dead letter queue {Dlq} contains {Depth} message(s) — manual investigation required", dlq, info.MessageCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not read depth of {Dlq}", dlq);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
