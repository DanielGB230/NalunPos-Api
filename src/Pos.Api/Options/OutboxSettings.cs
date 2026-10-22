using System.ComponentModel.DataAnnotations;

namespace Pos.Api.Options;

public class OutboxSettings
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
}
