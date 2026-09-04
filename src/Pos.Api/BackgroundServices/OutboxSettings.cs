namespace Pos.Api.BackgroundServices;

public class OutboxSettings
{
    public const string SectionName = "OutboxSettings";

    public int PollingIntervalSeconds { get; set; } = 10;
}
