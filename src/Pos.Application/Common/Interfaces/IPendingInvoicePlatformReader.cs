namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Resumen inmutable de factura pendiente de reconciliación fiscal cross-tenant.
/// </summary>
public sealed record PendingInvoiceSummary(
    Guid Id,
    Guid TenantId,
    string DocumentNumber,
    int ReconciliationAttempts,
    DateTime IssueDateUtc);

/// <summary>
/// Puerto de lectura de plataforma para descubrir facturas pendientes vencidas a través de todos los tenants.
/// Implementado en Pos.Infrastructure bajo alcance temporal de plataforma (SESSION_CONTEXT IsSuperAdmin=1).
/// </summary>
public interface IPendingInvoicePlatformReader
{
    Task<IReadOnlyList<PendingInvoiceSummary>> GetPendingInvoicesDueAsync(
        DateTime thresholdUtc,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken = default);
}
