using RabbitMQ.Client;

namespace Platform.Messaging.Topology;

/// <summary>
/// Декларативна топологія брокера: оголошується в коді під час старту кожного учасника (publisher і consumers).
/// Повторне оголошення з тими самими параметрами безпечне (ідемпотентне).
/// <code>
/// catalog-events (topic) ──"product.*"─────────► orders.catalog-sync    ──(reject/expire)──► catalog-events.dlx (direct) ──► orders.catalog-sync.dlq
///                         └─"product.renamed"──► reviews.catalog-sync ──(reject/expire)──► catalog-events.dlx (direct) ──► reviews.catalog-sync.dlq
/// </code>
/// Чому оголошується ВСЯ топологія, а не лише черга споживача: якщо Каталог опублікує подію до першого запуску
/// споживача, а черги ще немає, exchange відкине повідомлення (немає bindings). Тому publisher теж оголошує черги.
/// </summary>
public static class MessagingTopology
{
    public const string CatalogExchange = "catalog-events";
    public const string CatalogDeadLetterExchange = "catalog-events.dlx";

    public const string OrdersQueue = "orders.catalog-sync";
    public const string ReviewsQueue = "reviews.catalog-sync";

    public static string DeadLetterQueue(string queue) => queue + ".dlq";

    // Черга → шаблони маршрутизації (* — одне слово, # — будь-яка кількість слів)
    private static readonly IReadOnlyDictionary<string, string[]> Queues = new Dictionary<string, string[]>
    {
        [OrdersQueue] = ["product.*"],                    // довідник товарів Orders: назва, ціна, активність, склад
        [ReviewsQueue] = [CatalogRoutingKeys.Renamed]    // відгукам потрібна лише назва
    };

    public static IEnumerable<string> AllQueues => Queues.Keys;

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(CatalogExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        // direct (а не fanout): routing key = ім'я черги, тож кожна DLQ отримує лише «свої» повідомлення
        await channel.ExchangeDeclareAsync(CatalogDeadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);

        foreach (var (queue, bindingKeys) in Queues)
        {
            var args = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = CatalogDeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue
            };

            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: args, cancellationToken: ct);
            foreach (var key in bindingKeys)
                await channel.QueueBindAsync(queue, CatalogExchange, key, cancellationToken: ct);

            var dlq = DeadLetterQueue(queue);
            await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await channel.QueueBindAsync(dlq, CatalogDeadLetterExchange, routingKey: queue, cancellationToken: ct);
        }
    }
}
