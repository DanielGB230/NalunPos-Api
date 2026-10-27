using FluentValidation.TestHelper;
using Pos.Application.Common.Models;
using Pos.Application.Platform.Outbox.DTOs;
using Pos.Application.Platform.Outbox.Queries;
using Pos.Application.Tests.Support.Fakes;
using Xunit;

namespace Pos.Application.Tests.Platform.Outbox;

public class GetDeadLetterMessagesQueryTests
{
    private readonly GetDeadLetterMessagesQueryValidator _validator = new();

    [Fact]
    public void Validator_WhenPageSizeExceedsMax_HasValidationError()
    {
        var query = new GetDeadLetterMessagesQuery(PageNumber: 1, PageSize: 101);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validator_WhenPageNumberLessThan1_HasValidationError()
    {
        var query = new GetDeadLetterMessagesQuery(PageNumber: 0, PageSize: 50);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Fact]
    public void Validator_WhenValidParameters_PassesValidation()
    {
        var query = new GetDeadLetterMessagesQuery(PageNumber: 1, PageSize: 100);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Handler_WhenCalled_ReturnsPagedResultFromReader()
    {
        var fakeReader = new FakeOutboxPlatformReader();
        fakeReader.Messages.Add(new DeadLetterMessageDto(Guid.NewGuid(), null, "TypeA", "{}", DateTimeOffset.UtcNow, 3, "Error A"));
        fakeReader.Messages.Add(new DeadLetterMessageDto(Guid.NewGuid(), null, "TypeB", "{}", DateTimeOffset.UtcNow, 3, "Error B"));

        var handler = new GetDeadLetterMessagesQueryHandler(fakeReader);

        var query = new GetDeadLetterMessagesQuery(PageNumber: 1, PageSize: 50);
        var result = await handler.HandleAsync(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.PageNumber);
        Assert.Equal(50, result.Value.PageSize);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
    }
}
