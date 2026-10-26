using System.ComponentModel.DataAnnotations;

namespace Pos.Api.Options;

public class OutboxSettings : IValidatableObject
{
    public const string SectionName = "OutboxSettings";

    [Range(1, 3600, ErrorMessage = "PollingIntervalSeconds debe estar entre 1 y 3600 segundos.")]
    public int PollingIntervalSeconds { get; set; } = 10;

    [Range(1, 1000, ErrorMessage = "BatchSize debe estar entre 1 y 1000.")]
    public int BatchSize { get; set; } = 20;

    [Range(1, 20, ErrorMessage = "MaxRetries debe estar entre 1 y 20.")]
    public int MaxRetries { get; set; } = 3;

    [Range(1, 300, ErrorMessage = "BaseRetryIntervalSeconds debe estar entre 1 y 300.")]
    public int BaseRetryIntervalSeconds { get; set; } = 2;

    [Range(5, 3600, ErrorMessage = "LeaseDurationSeconds debe estar entre 5 y 3600.")]
    public int LeaseDurationSeconds { get; set; } = 60;

    [Range(1, 60, ErrorMessage = "MessageTimeoutSeconds debe estar entre 1 y 60.")]
    public int MessageTimeoutSeconds { get; set; } = 2;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BatchSize * MessageTimeoutSeconds >= LeaseDurationSeconds)
        {
            yield return new ValidationResult(
                $"Invariante violada: BatchSize ({BatchSize}) × MessageTimeoutSeconds ({MessageTimeoutSeconds}) = {BatchSize * MessageTimeoutSeconds}s debe ser estrictamente menor que LeaseDurationSeconds ({LeaseDurationSeconds}s) para prevenir expiración prematura de leases en lotes bajo procesamiento activo.",
                new[] { nameof(LeaseDurationSeconds), nameof(BatchSize), nameof(MessageTimeoutSeconds) });
        }
    }
}
