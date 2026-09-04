using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Infrastructure.Multitenancy;

/// <summary>
/// Interceptor de SaveChanges de EF Core que asigna automáticamente el TenantId
/// a las entidades que implementan ITenantOwnedEntity al momento de ser creadas.
/// </summary>
public class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenantContext _currentTenantContext;

    public TenantSaveChangesInterceptor(ICurrentTenantContext currentTenantContext)
    {
        _currentTenantContext = currentTenantContext;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SetTenantId(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetTenantId(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SetTenantId(DbContext? context)
    {
        if (context == null) return;

        var tenantId = _currentTenantContext.TenantId;
        if (!tenantId.HasValue) return;

        var entries = context.ChangeTracker.Entries<ITenantOwnedEntity>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in entries)
        {
            // Asignación de TenantId reflexiva o vía propiedad si aplica en el futuro
            var property = entry.Property("TenantId");
            if (property != null && (property.CurrentValue == null || (Guid)property.CurrentValue == Guid.Empty))
            {
                property.CurrentValue = tenantId.Value;
            }
        }
    }
}
