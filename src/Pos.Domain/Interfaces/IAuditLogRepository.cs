using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? tableName,
        Guid? userId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}
