using Pos.Application.Common.Attributes;
using Pos.Application.Common.Behaviors;
using Pos.Application.Common.Interfaces;
using Pos.Application.Tests.Support.Fakes;
using Pos.Domain.Common;
using Xunit;

namespace Pos.Application.Tests.Common.Behaviors;

public class TenantRequirementBehaviorTests
{
    [RequiresTenant]
    private sealed record CommandWithTenantRequirement : ICommand<Result<string>>;

    private sealed record CommandWithoutTenantRequirement : ICommand<Result<string>>;

    [RequiresTenant]
    private sealed record VoidCommandWithTenantRequirement : ICommand<Result>;

    private sealed class StubCommandHandler<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public bool WasExecuted { get; private set; }
        public TResponse ResponseToReturn { get; set; } = default!;

        public Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            return Task.FromResult(ResponseToReturn);
        }
    }

    [Fact]
    public async Task HandleAsync_WhenRequiresTenantAndTenantIdIsNull_ReturnsFailureAndDoesNotCallHandler()
    {
        // Arrange
        var fakeTenantContext = new FakeCurrentTenantContext { TenantId = null };
        var stubHandler = new StubCommandHandler<CommandWithTenantRequirement, Result<string>>
        {
            ResponseToReturn = Result.Ok("success")
        };
        var behavior = new TenantRequirementBehavior<CommandWithTenantRequirement, Result<string>>(stubHandler, fakeTenantContext);
        var command = new CommandWithTenantRequirement();

        // Act
        var result = await behavior.HandleAsync(command);

        // Assert
        Assert.False(stubHandler.WasExecuted);
        Assert.True(result.IsFailure);
        Assert.Equal("tenant.required", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task HandleAsync_WhenRequiresTenantAndTenantIdIsEmptyGuid_ReturnsFailureAndDoesNotCallHandler()
    {
        // Arrange
        var fakeTenantContext = new FakeCurrentTenantContext { TenantId = Guid.Empty };
        var stubHandler = new StubCommandHandler<CommandWithTenantRequirement, Result<string>>
        {
            ResponseToReturn = Result.Ok("success")
        };
        var behavior = new TenantRequirementBehavior<CommandWithTenantRequirement, Result<string>>(stubHandler, fakeTenantContext);
        var command = new CommandWithTenantRequirement();

        // Act
        var result = await behavior.HandleAsync(command);

        // Assert
        Assert.False(stubHandler.WasExecuted);
        Assert.True(result.IsFailure);
        Assert.Equal("tenant.required", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenRequiresTenantAndTenantIdIsValid_ExecutesHandler()
    {
        // Arrange
        var validTenantId = Guid.NewGuid();
        var fakeTenantContext = new FakeCurrentTenantContext { TenantId = validTenantId };
        var stubHandler = new StubCommandHandler<CommandWithTenantRequirement, Result<string>>
        {
            ResponseToReturn = Result.Ok("ejecutado con éxito")
        };
        var behavior = new TenantRequirementBehavior<CommandWithTenantRequirement, Result<string>>(stubHandler, fakeTenantContext);
        var command = new CommandWithTenantRequirement();

        // Act
        var result = await behavior.HandleAsync(command);

        // Assert
        Assert.True(stubHandler.WasExecuted);
        Assert.True(result.IsSuccess);
        Assert.Equal("ejecutado con éxito", result.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenCommandDoesNotRequireTenant_PassesThroughEvenWithoutTenant()
    {
        // Arrange: Sin tenant (null) pero el comando no tiene [RequiresTenant]
        var fakeTenantContext = new FakeCurrentTenantContext { TenantId = null };
        var stubHandler = new StubCommandHandler<CommandWithoutTenantRequirement, Result<string>>
        {
            ResponseToReturn = Result.Ok("operacion global")
        };
        var behavior = new TenantRequirementBehavior<CommandWithoutTenantRequirement, Result<string>>(stubHandler, fakeTenantContext);
        var command = new CommandWithoutTenantRequirement();

        // Act
        var result = await behavior.HandleAsync(command);

        // Assert
        Assert.True(stubHandler.WasExecuted);
        Assert.True(result.IsSuccess);
        Assert.Equal("operacion global", result.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenRequiresTenantAndResponseTypeIsPlainResult_ReturnsFailedResult()
    {
        // Arrange
        var fakeTenantContext = new FakeCurrentTenantContext { TenantId = null };
        var stubHandler = new StubCommandHandler<VoidCommandWithTenantRequirement, Result>
        {
            ResponseToReturn = Result.Success()
        };
        var behavior = new TenantRequirementBehavior<VoidCommandWithTenantRequirement, Result>(stubHandler, fakeTenantContext);
        var command = new VoidCommandWithTenantRequirement();

        // Act
        var result = await behavior.HandleAsync(command);

        // Assert
        Assert.False(stubHandler.WasExecuted);
        Assert.True(result.IsFailure);
        Assert.Equal("tenant.required", result.Error.Code);
    }

    [Fact]
    public void RequireTenantId_WhenTenantIdIsNull_ThrowsInvalidOperationException()
    {
        var fakeContext = new FakeCurrentTenantContext { TenantId = null };

        var ex = Assert.Throws<InvalidOperationException>(() => fakeContext.RequireTenantId());
        Assert.Contains("TenantId is required", ex.Message);
    }

    [Fact]
    public void RequireTenantId_WhenTenantIdIsEmpty_ThrowsInvalidOperationException()
    {
        var fakeContext = new FakeCurrentTenantContext { TenantId = Guid.Empty };

        var ex = Assert.Throws<InvalidOperationException>(() => fakeContext.RequireTenantId());
        Assert.Contains("TenantId is required", ex.Message);
    }

    [Fact]
    public void RequireTenantId_WhenTenantIdIsValid_ReturnsTenantGuid()
    {
        var expected = Guid.NewGuid();
        var fakeContext = new FakeCurrentTenantContext { TenantId = expected };

        var actual = fakeContext.RequireTenantId();

        Assert.Equal(expected, actual);
    }
}
