using System.ComponentModel.DataAnnotations;

namespace Pos.Api.Options;

public class InvoiceReconciliationSettings
{
    [Range(5, 86400, ErrorMessage = "IntervalSeconds debe estar entre 5 y 86400 segundos.")]
    public int IntervalSeconds { get; set; } = 300;

    [Range(1, 1440, ErrorMessage = "ThresholdMinutes debe ser mayor a 0.")]
    public int ThresholdMinutes { get; set; } = 5;

    [Range(1, 100, ErrorMessage = "MaxReconciliationAttempts debe estar entre 1 y 100.")]
    public int MaxReconciliationAttempts { get; set; } = Pos.Domain.Common.InvoiceConstants.DefaultMaxReconciliationAttempts;

    [Range(1, 1000, ErrorMessage = "BatchSize debe estar entre 1 y 1000.")]
    public int BatchSize { get; set; } = 50;
}
