using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Enums;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence;

/// <summary>
/// Implementación de infraestructura del lector de plataforma cross-tenant para facturas pendientes.
/// Activa de manera temporal y acotada el alcance de SuperAdmin (SESSION_CONTEXT IsSuperAdmin=1)
/// para consultar a través de RLS en SQL Server 2022, garantizando restauración determinista en bloque try/finally.
/// </summary>
public sealed class PendingInvoicePlatformReader : IPendingInvoicePlatformReader
{
    private readonly PosDbContext _dbContext;
    private readonly ITenantSetter _tenantSetter;

    public PendingInvoicePlatformReader(PosDbContext dbContext, ITenantSetter tenantSetter)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantSetter = tenantSetter ?? throw new ArgumentNullException(nameof(tenantSetter));
    }

    public async Task<IReadOnlyList<PendingInvoiceSummary>> GetPendingInvoicesDueAsync(
        DateTime thresholdUtc,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        _tenantSetter.SetSuperAdmin(true);
        try
        {
            var invoices = await _dbContext.Invoices
                .IgnoreQueryFilters()
                .Where(i => i.Status == InvoiceStatus.Pending &&
                            i.ReconciliationAttempts < maxAttempts &&
                            i.IssueDateUtc <= thresholdUtc)
                .OrderBy(i => i.IssueDateUtc)
                .Take(Math.Max(1, batchSize))
                .Select(i => new PendingInvoiceSummary(
                    i.Id,
                    i.TenantId,
                    i.DocumentNumber,
                    i.ReconciliationAttempts,
                    i.IssueDateUtc))
                .ToListAsync(cancellationToken);

            return invoices;
        }
        finally
        {
            _tenantSetter.SetSuperAdmin(false);
        }
    }
}
