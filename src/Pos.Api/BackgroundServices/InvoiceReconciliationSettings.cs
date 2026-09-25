namespace Pos.Api.BackgroundServices;

public class InvoiceReconciliationSettings
{
    public int IntervalSeconds { get; set; } = 300;
    public int ThresholdMinutes { get; set; } = 5;
    public int MaxReconciliationAttempts { get; set; } = 5;
}
