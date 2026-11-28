using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.Messaging.Topology;

/// <summary>Оголошує топологію під час старту застосунку (до початку обробки запитів і споживання).</summary>
public sealed class TopologyInitializer(IConnection connection, ILogger<TopologyInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await MessagingTopology.DeclareAsync(channel, ct);
        logger.LogInformation("Messaging topology declared: exchange {Exchange}, queues {Queues}",
            MessagingTopology.CatalogExchange, string.Join(", ", MessagingTopology.AllQueues));
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
