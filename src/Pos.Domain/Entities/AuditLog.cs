using Pos.Domain.Common;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Entidad para Registro de Auditoría de cambios en el sistema POS.
/// </summary>
public class AuditLog : Entity<Guid>, IPlatformScopedEntity
{
    public Guid TenantId { get; private set; }
    public string TableName { get; private set; } = string.Empty;
    public string RecordId { get; private set; } = string.Empty;
    public AuditAction Action { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }

    private AuditLog()
    {
    }

    private AuditLog(
        Guid id,
        Guid tenantId,
        string tableName,
        string recordId,
        AuditAction action,
        Guid? userId,
        string? oldValues,
        string? newValues,
        TimeProvider timeProvider) : base(id)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new DomainException("El nombre de la tabla auditada es requerido.");
        }

        if (string.IsNullOrWhiteSpace(recordId))
        {
            throw new DomainException("El ID del registro auditado es requerido.");
        }

        TenantId = tenantId;
        TableName = tableName.Trim();
        RecordId = recordId.Trim();
        Action = action;
        UserId = userId;
        OldValues = oldValues;
        NewValues = newValues;
        TimestampUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public static AuditLog Create(
        Guid tenantId,
        string tableName,
        string recordId,
        AuditAction action,
        TimeProvider timeProvider)
    {
        return Create(tenantId, tableName, recordId, action, null, null, null, timeProvider);
    }

    public static AuditLog Create(
        Guid tenantId,
        string tableName,
        string recordId,
        AuditAction action,
        Guid? userId,
        string? oldValues,
        string? newValues,
        TimeProvider timeProvider)
    {
        return new AuditLog(Guid.NewGuid(), tenantId, tableName, recordId, action, userId, oldValues, newValues, timeProvider);
    }
}
