using System.ComponentModel.DataAnnotations;

namespace Pos.Api.Options;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Required]
    public PolicyOptions AuthPolicy { get; set; } = new() { PermitLimit = 10, WindowMinutes = 1 };

    [Required]
    public SlidingPolicyOptions PosCheckoutPolicy { get; set; } = new() { PermitLimit = 120, WindowMinutes = 1, SegmentsPerWindow = 6 };

    [Required]
    public PolicyOptions SensitiveOperationsPolicy { get; set; } = new() { PermitLimit = 60, WindowMinutes = 1 };

    [Required]
    public PolicyOptions GlobalApiPolicy { get; set; } = new() { PermitLimit = 300, WindowMinutes = 1 };
}

public class PolicyOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int WindowMinutes { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; } = 0;
}

public class SlidingPolicyOptions : PolicyOptions
{
    [Range(1, int.MaxValue)]
    public int SegmentsPerWindow { get; set; } = 6;
}
