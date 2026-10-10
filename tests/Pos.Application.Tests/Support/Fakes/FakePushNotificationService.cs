namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;

public sealed class FakePushNotificationService : IPushNotificationService
{
    public int SentCount { get; private set; }
    public Task SendToUserAsync(Guid userId, string title, string message, CancellationToken cancellationToken = default)
    {
        SentCount++;
        return Task.CompletedTask;
    }
}
