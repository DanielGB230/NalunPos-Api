namespace Pos.Domain.Common;

/// <summary>
/// Constantes de negocio compartidas para el dominio de facturación electrónica.
/// </summary>
public static class InvoiceConstants
{
    /// <summary>
    /// Número máximo de reintentos por defecto antes de marcar una factura como RequiresManualIntervention.
    /// </summary>
    public const int DefaultMaxReconciliationAttempts = 5;

    /// <summary>
    /// Longitud máxima para la descripción/motivo de la intervención manual.
    /// </summary>
    public const int MaxManualInterventionReasonLength = 500;
}
