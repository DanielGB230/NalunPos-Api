namespace Pos.Application.Tests.Common.Behaviors;

using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Behaviors;
using Pos.Application.Common.Interfaces;
using Pos.Application.Tests.Support.Fakes;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Xunit;

public class AuthorizationBehaviorTests
{
    [HasPermission("Invoices.Reconcile")]
    private sealed record SampleProtectedCommand : ICommand<Result<bool>>;

    [HasPermission("Invoices.Reconcile")]
    private sealed record AnotherProtectedCommand : ICommand<Result<bool>>;

    [HasPermission("Invoices.View")]
    private sealed record SampleProtectedQuery : IQuery<Result<bool>>;

    private sealed class SampleCommandHandler<T> : ICommandHandler<T, Result<bool>>
        where T : ICommand<Result<bool>>
    {
        public bool Handled { get; private set; }

        public Task<Result<bool>> HandleAsync(T request, CancellationToken cancellationToken = default)
        {
            Handled = true;
            return Task.FromResult(Result.Ok(true));
        }
    }

    private sealed class SampleQueryHandler<T> : IQueryHandler<T, Result<bool>>
        where T : IQuery<Result<bool>>
    {
        public bool Handled { get; private set; }

        public Task<Result<bool>> HandleAsync(T request, CancellationToken cancellationToken = default)
        {
            Handled = true;
            return Task.FromResult(Result.Ok(true));
        }
    }

    [Fact]
    public async Task AuthorizationBehavior_ThrowsUnauthorized_WhenOutsideHttpAndNoSystemScopeActive()
    {
        // Arrange: fuera de HTTP (sin UserId), pero sin ISystemExecutionScope
        var userService = new FakeCurrentUserService { UserId = null };
        var userRepo = new FakeUserRepository();
        var permissions = new FakeCurrentUserPermissions();
        var systemScope = new FakeSystemExecutionScope();
        var innerHandler = new SampleCommandHandler<SampleProtectedCommand>();

        var behavior = new AuthorizationBehavior<SampleProtectedCommand, Result<bool>>(
            userService,
            userRepo,
            permissions,
            systemScope,
            innerHandler);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedDomainException>(() =>
            behavior.HandleAsync(new SampleProtectedCommand()));

        Assert.False(innerHandler.Handled);
    }

    [Fact]
    public async Task AuthorizationBehavior_ThrowsForbidden_WhenSystemScopeActiveButCommandNotAllowed()
    {
        // Arrange: Worker activa ámbito pero solo autoriza AnotherProtectedCommand
        var userService = new FakeCurrentUserService { UserId = null };
        var userRepo = new FakeUserRepository();
        var permissions = new FakeCurrentUserPermissions();
        var systemScope = new FakeSystemExecutionScope();
        var innerHandler = new SampleCommandHandler<SampleProtectedCommand>();

        using (systemScope.BeginScope("TestWorker", new[] { typeof(AnotherProtectedCommand) }))
        {
            var behavior = new AuthorizationBehavior<SampleProtectedCommand, Result<bool>>(
                userService,
                userRepo,
                permissions,
                systemScope,
                innerHandler);

            // Act & Assert: Fail-Closed para comandos no declarados en el ámbito
            var ex = await Assert.ThrowsAsync<ForbiddenDomainException>(() =>
                behavior.HandleAsync(new SampleProtectedCommand()));

            Assert.Contains("TestWorker", ex.Message);
            Assert.Contains("SampleProtectedCommand", ex.Message);
            Assert.False(innerHandler.Handled);
        }
    }

    [Fact]
    public async Task AuthorizationBehavior_ExecutesInnerHandler_WhenSystemScopeActiveAndCommandAllowed()
    {
        // Arrange: Worker activa ámbito con SampleProtectedCommand permitido
        var userService = new FakeCurrentUserService { UserId = null };
        var userRepo = new FakeUserRepository();
        var permissions = new FakeCurrentUserPermissions();
        var systemScope = new FakeSystemExecutionScope();
        var innerHandler = new SampleCommandHandler<SampleProtectedCommand>();

        using (systemScope.BeginScope("InvoiceReconciliationWorker", new[] { typeof(SampleProtectedCommand) }))
        {
            var behavior = new AuthorizationBehavior<SampleProtectedCommand, Result<bool>>(
                userService,
                userRepo,
                permissions,
                systemScope,
                innerHandler);

            // Act
            var result = await behavior.HandleAsync(new SampleProtectedCommand());

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(innerHandler.Handled);
        }
    }

    [Fact]
    public async Task AuthorizationQueryBehavior_ThrowsUnauthorized_WhenOutsideHttpAndNoSystemScopeActive()
    {
        var userService = new FakeCurrentUserService { UserId = null };
        var userRepo = new FakeUserRepository();
        var permissions = new FakeCurrentUserPermissions();
        var systemScope = new FakeSystemExecutionScope();
        var innerHandler = new SampleQueryHandler<SampleProtectedQuery>();

        var behavior = new AuthorizationQueryBehavior<SampleProtectedQuery, Result<bool>>(
            userService,
            userRepo,
            permissions,
            systemScope,
            innerHandler);

        await Assert.ThrowsAsync<UnauthorizedDomainException>(() =>
            behavior.HandleAsync(new SampleProtectedQuery()));

        Assert.False(innerHandler.Handled);
    }

    [Fact]
    public async Task AuthorizationQueryBehavior_ThrowsForbidden_WhenSystemScopeActiveButQueryNotAllowed()
    {
        var userService = new FakeCurrentUserService { UserId = null };
        var userRepo = new FakeUserRepository();
        var permissions = new FakeCurrentUserPermissions();
        var systemScope = new FakeSystemExecutionScope();
        var innerHandler = new SampleQueryHandler<SampleProtectedQuery>();

        using (systemScope.BeginScope("TestWorker", Type.EmptyTypes))
        {
            var behavior = new AuthorizationQueryBehavior<SampleProtectedQuery, Result<bool>>(
                userService,
                userRepo,
                permissions,
                systemScope,
                innerHandler);

            var ex = await Assert.ThrowsAsync<ForbiddenDomainException>(() =>
                behavior.HandleAsync(new SampleProtectedQuery()));

            Assert.Contains("TestWorker", ex.Message);
            Assert.Contains("SampleProtectedQuery", ex.Message);
            Assert.False(innerHandler.Handled);
        }
    }

    [Fact]
    public async Task AuthorizationQueryBehavior_ExecutesInnerHandler_WhenSystemScopeActiveAndQueryAllowed()
    {
        var userService = new FakeCurrentUserService { UserId = null };
        var userRepo = new FakeUserRepository();
        var permissions = new FakeCurrentUserPermissions();
        var systemScope = new FakeSystemExecutionScope();
        var innerHandler = new SampleQueryHandler<SampleProtectedQuery>();

        using (systemScope.BeginScope("ReportingWorker", new[] { typeof(SampleProtectedQuery) }))
        {
            var behavior = new AuthorizationQueryBehavior<SampleProtectedQuery, Result<bool>>(
                userService,
                userRepo,
                permissions,
                systemScope,
                innerHandler);

            var result = await behavior.HandleAsync(new SampleProtectedQuery());

            Assert.True(result.IsSuccess);
            Assert.True(innerHandler.Handled);
        }
    }
}
