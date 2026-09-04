using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class AgentActionRecordTests
{
    private static readonly Guid TestTenantId = Guid.NewGuid();

    [Fact]
    public void CreateAgentActionRecordShouldInstantiatePendingApprovalRecord()
    {
        // Arrange
        string agentId = "InventoryBot";
        string actionType = "CreatePurchaseOrder";
        string payloadJson = "{\"ProductId\":\"" + Guid.NewGuid() + "\",\"Quantity\":50}";

        // Act
        var record = AgentActionRecord.Create(TestTenantId, agentId, actionType, payloadJson, RiskLevel.High);

        // Assert
        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(TestTenantId, record.TenantId);
        Assert.Equal(agentId, record.AgentId);
        Assert.Equal(actionType, record.ProposedActionType);
        Assert.Equal(payloadJson, record.PayloadJson);
        Assert.Equal(RiskLevel.High, record.RiskLevel);
        Assert.Equal(AgentActionStatus.PendingApproval, record.Status);
        Assert.Null(record.ReviewedByUserId);
        Assert.Single(record.DomainEvents);
    }

    [Fact]
    public void ApproveRecordByHumanUserShouldChangeStatusToApproved()
    {
        // Arrange
        var record = AgentActionRecord.Create(TestTenantId, "Bot1", "Action1", "{}", RiskLevel.Medium);
        Guid reviewerUserId = Guid.NewGuid();

        // Act
        record.Approve(reviewerUserId);

        // Assert
        Assert.Equal(AgentActionStatus.Approved, record.Status);
        Assert.Equal(reviewerUserId, record.ReviewedByUserId);
        Assert.NotNull(record.ReviewedAtUtc);
        Assert.Equal(2, record.DomainEvents.Count);
    }

    [Fact]
    public void RejectRecordByHumanUserShouldChangeStatusToRejected()
    {
        // Arrange
        var record = AgentActionRecord.Create(TestTenantId, "Bot1", "Action1", "{}", RiskLevel.Low);
        Guid reviewerUserId = Guid.NewGuid();

        // Act
        record.Reject(reviewerUserId);

        // Assert
        Assert.Equal(AgentActionStatus.Rejected, record.Status);
        Assert.Equal(reviewerUserId, record.ReviewedByUserId);
    }

    [Fact]
    public void MarkAsExecutedWithoutPriorApprovalShouldThrowInvalidAgentActionStateException()
    {
        // Arrange
        var record = AgentActionRecord.Create(TestTenantId, "Bot1", "Action1", "{}", RiskLevel.High);

        // Act & Assert
        Assert.Throws<InvalidAgentActionStateException>(() =>
            record.MarkAsExecuted());
    }
}
