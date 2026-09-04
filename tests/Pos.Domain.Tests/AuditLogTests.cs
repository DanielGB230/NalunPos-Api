using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class AuditLogTests
{
    [Fact]
    public void CreateAuditLogWithValidParametersShouldInstantiateLog()
    {
        // Arrange
        string tableName = "Products";
        string recordId = Guid.NewGuid().ToString();
        Guid userId = Guid.NewGuid();

        // Act
        var audit = AuditLog.Create(tableName, recordId, AuditAction.Update, userId, "{\"Price\":100}", "{\"Price\":120}");

        // Assert
        Assert.NotEqual(Guid.Empty, audit.Id);
        Assert.Equal(tableName, audit.TableName);
        Assert.Equal(recordId, audit.RecordId);
        Assert.Equal(AuditAction.Update, audit.Action);
        Assert.Equal(userId, audit.UserId);
    }

    [Fact]
    public void CreateAuditLogWithEmptyTableShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            AuditLog.Create("", "123", AuditAction.Insert));
    }
}
