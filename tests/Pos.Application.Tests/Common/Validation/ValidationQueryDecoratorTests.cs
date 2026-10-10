using Pos.Application.Tests.Support.Fakes;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Xunit;

namespace Pos.Application.Tests.Common.Validation;

public class ValidationQueryDecoratorTests
{
    [Fact]
    public async Task QueryHandler_ResolvedFromServiceCollection_InvalidAndUnauthorized_ThrowsUnauthorizedDomainException()
    {
        var (provider, currentUserService, _, _) = CreateTestServiceProvider();
        var queryHandler = provider.GetRequiredService<IQueryHandler<TestResultQuery, Result<string>>>();

        // (a) Inválida + no autorizado => UnauthorizedDomainException (lanzado por AuthorizationQueryBehavior antes de validar)
        currentUserService.UserId = null;
        var invalidQuery = new TestResultQuery("");

        await Assert.ThrowsAsync<UnauthorizedDomainException>(() => queryHandler.HandleAsync(invalidQuery));
    }

    [Fact]
    public async Task QueryHandler_ResolvedFromServiceCollection_InvalidAndAuthorized_ReturnsFailedResultWithFieldErrors()
    {
        var (provider, currentUserService, userRepository, _) = CreateTestServiceProvider();
        var queryHandler = provider.GetRequiredService<IQueryHandler<TestResultQuery, Result<string>>>();

        // (b) Inválida + autorizado => Result fallido con FieldErrors
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        currentUserService.UserId = userId;
        userRepository.UserToReturn = User.Create("test@example.com", "hash", roleId, tenantId, "Test", "User");

        var invalidQuery = new TestResultQuery("");
        var result = await queryHandler.HandleAsync(invalidQuery);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Error", result.Error.Code);
        Assert.NotNull(result.Error.Errors);
        Assert.True(result.Error.Errors.Values.ContainsKey("SearchText"));
        Assert.Equal("SearchText es requerido", result.Error.Errors.Values["SearchText"][0]);
    }

    [Fact]
    public async Task QueryHandler_ResolvedFromServiceCollection_ValidAndAuthorized_ReachesHandler()
    {
        var (provider, currentUserService, userRepository, _) = CreateTestServiceProvider();
        var queryHandler = provider.GetRequiredService<IQueryHandler<TestResultQuery, Result<string>>>();

        // (c) Válida + autorizado => llega al handler y retorna éxito
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        currentUserService.UserId = userId;
        userRepository.UserToReturn = User.Create("test@example.com", "hash", roleId, tenantId, "Test", "User");

        var validQuery = new TestResultQuery("búsqueda válida");
        var result = await queryHandler.HandleAsync(validQuery);

        Assert.True(result.IsSuccess);
        Assert.Equal("Resultado: búsqueda válida", result.Value);
    }

    [Fact]
    public async Task QueryHandler_NonResultResponse_InvalidAndAuthorized_ThrowsValidationException()
    {
        var (provider, currentUserService, userRepository, _) = CreateTestServiceProvider();
        var queryHandler = provider.GetRequiredService<IQueryHandler<NonResultQuery, Guid>>();

        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        currentUserService.UserId = userId;
        userRepository.UserToReturn = User.Create("test@example.com", "hash", roleId, tenantId, "Test", "User");

        var invalidQuery = new NonResultQuery(Guid.Empty);

        await Assert.ThrowsAsync<ValidationException>(() => queryHandler.HandleAsync(invalidQuery));
    }

    // ── Helper para construir IServiceProvider real con AddApplicationServices ────

    private static (
        IServiceProvider Provider,
        FakeCurrentUserService CurrentUserService,
        FakeUserRepository UserRepository,
        FakeCurrentUserPermissions CurrentUserPermissions) CreateTestServiceProvider()
    {
        var services = new ServiceCollection();

        var currentUserService = new FakeCurrentUserService();
        var userRepository = new FakeUserRepository();
        var currentUserPermissions = new FakeCurrentUserPermissions();

        services.AddSingleton<ICurrentUserService>(currentUserService);
        services.AddSingleton<IUserRepository>(userRepository);
        services.AddSingleton<ICurrentUserPermissions>(currentUserPermissions);

        // Registrar handlers y validadores de prueba explícitamente como IQueryHandler<,> e IValidator<>
        services.AddScoped<IQueryHandler<TestResultQuery, Result<string>>, TestResultQueryHandler>();
        services.AddScoped<IValidator<TestResultQuery>, TestResultQueryValidator>();

        services.AddScoped<IQueryHandler<NonResultQuery, Guid>, NonResultQueryHandler>();
        services.AddScoped<IValidator<NonResultQuery>, NonResultQueryValidator>();

        // Registrar los servicios de la aplicación (aplica decoradores sobre IQueryHandler<,>)
        services.AddApplicationServices();

        return (services.BuildServiceProvider(), currentUserService, userRepository, currentUserPermissions);
    }

    // ── Tipos y Fakes auxiliares para la prueba ──────────────────────────────

    [AuthenticatedOnly]
    public record TestResultQuery(string SearchText) : IQuery<Result<string>>;

    public sealed class TestResultQueryHandler : IQueryHandler<TestResultQuery, Result<string>>
    {
        public Task<Result<string>> HandleAsync(TestResultQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok($"Resultado: {query.SearchText}"));
    }

    public sealed class TestResultQueryValidator : AbstractValidator<TestResultQuery>
    {
        public TestResultQueryValidator()
        {
            RuleFor(x => x.SearchText).NotEmpty().WithMessage("SearchText es requerido");
        }
    }

    [AuthenticatedOnly]
    public record NonResultQuery(Guid Id) : IQuery<Guid>;

    public sealed class NonResultQueryHandler : IQueryHandler<NonResultQuery, Guid>
    {
        public Task<Guid> HandleAsync(NonResultQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(query.Id != Guid.Empty ? query.Id : Guid.NewGuid());
    }

    public sealed class NonResultQueryValidator : AbstractValidator<NonResultQuery>
    {
        public NonResultQueryValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("Id es requerido");
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? UserToReturn { get; set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(UserToReturn);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(UserToReturn);

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Update(User user) { }

        public Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm,
            bool? isActive = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<User> emptyList = Array.Empty<User>();
            return Task.FromResult((Items: emptyList, TotalCount: 0));
        }
    }

}
