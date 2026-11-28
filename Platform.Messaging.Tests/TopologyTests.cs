using Platform.Messaging.Topology;

namespace Platform.Messaging.Tests;

public class TopologyTests
{
    [Fact]
    public void EveryConsumerQueue_HasDeadLetterQueueName()
    {
        Assert.Equal("orders.catalog-sync.dlq", MessagingTopology.DeadLetterQueue(MessagingTopology.OrdersQueue));
        Assert.Equal("reviews.catalog-sync.dlq", MessagingTopology.DeadLetterQueue(MessagingTopology.ReviewsQueue));
    }

    [Fact]
    public void AllQueues_ContainsBothConsumers()
    {
        var queues = MessagingTopology.AllQueues.ToList();

        Assert.Contains(MessagingTopology.OrdersQueue, queues);
        Assert.Contains(MessagingTopology.ReviewsQueue, queues);
    }
}
