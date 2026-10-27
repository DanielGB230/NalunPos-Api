using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Platform.Outbox.DTOs;

namespace Pos.Application.Tests.Support.Fakes;

public sealed class FakeOutboxPlatformReader : IOutboxPlatformReader
{
    public List<DeadLetterMessageDto> Messages { get; } = [];

    public Task<PagedResult<DeadLetterMessageDto>> GetDeadLetterMessagesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = Messages.Count;
        var skip = (pageNumber - 1) * pageSize;
        var pagedItems = Messages.Skip(skip).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<DeadLetterMessageDto>(pagedItems, pageNumber, pageSize, totalCount));
    }

    public Task<int> ReprocessDeadLetterMessagesAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken = default)
    {
        var targetIds = messageIds.ToHashSet();
        var removed = Messages.RemoveAll(m => targetIds.Contains(m.Id));
        return Task.FromResult(removed);
    }
}
