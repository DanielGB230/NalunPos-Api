using Pos.Application.IntegrationEvents.Contracts.V1;
using Pos.Infrastructure.Persistence.Outbox;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class OutboxPatternTests
{
    [Fact]
    public void CreateOutboxMessageShouldInstantiatePendingOutboxMessage()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        string type = typeof(SaleCompletedIntegrationEventV1).AssemblyQualifiedName!;
        string content = "{\"SaleId\":\"" + Guid.NewGuid() + "\",\"TotalAmount\":150.00}";
        DateTimeOffset occurredOn = DateTimeOffset.UtcNow;

        // Act
        var message = OutboxMessage.Create(id, type, content, occurredOn);

        // Assert
        Assert.Equal(id, message.Id);
        Assert.Equal(type, message.Type);
        Assert.Equal(content, message.Content);
        Assert.Null(message.ProcessedOnUtc);
        Assert.Null(message.Error);
    }

    [Fact]
    public void MarkAsProcessedShouldSetProcessedOnUtc()
    {
        // Arrange
        var message = OutboxMessage.Create(Guid.NewGuid(), "SampleType", "{}", DateTimeOffset.UtcNow);

        // Act
        message.MarkAsProcessed();

        // Assert
        Assert.NotNull(message.ProcessedOnUtc);
        Assert.Null(message.Error);
    }
}
