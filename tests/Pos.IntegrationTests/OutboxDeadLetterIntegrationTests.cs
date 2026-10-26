using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pos.Api.Contracts.Requests;
using Pos.Api.Contracts.Responses;
using Pos.Application.Common.Authorization;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Outbox;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class OutboxDeadLetterIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _anonymousClient = null!;

    public OutboxDeadLetterIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _fixture.ConnectionString);
                builder.UseSetting("SuperAdminSettings:Email", "superadmin_deadletter@test.com");
                builder.UseSetting("SuperAdminSettings:Password", "SuperSecurePassword123!");
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Admin");
                builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                builder.UseSetting("JwtSettings:Audience", "NalunPosClients");

                builder.ConfigureTestServices(services =>
                {
                    // Desactivar background services durante pruebas de integración
                    var outboxDescriptor = services.FirstOrDefault(d =>
                        d.ImplementationType == typeof(Pos.Api.BackgroundServices.OutboxProcessorBackgroundService));
                    if (outboxDescriptor != null) services.Remove(outboxDescriptor);

                    var reconciliationDescriptor = services.FirstOrDefault(d =>
                        d.ImplementationType == typeof(Pos.Api.BackgroundServices.InvoiceReconciliationBackgroundService));
                    if (reconciliationDescriptor != null) services.Remove(reconciliationDescriptor);

                    // Reemplazar PosDbContext para que use la base de datos SQL Server del fixture
                    services.RemoveAll<DbContextOptions<PosDbContext>>();
                    services.RemoveAll<DbContextOptions>();
                    var efConfigs = services
                        .Where(d => d.ServiceType.IsGenericType &&
                                    d.ServiceType.GetGenericTypeDefinition().FullName == "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1" &&
                                    d.ServiceType.GetGenericArguments()[0] == typeof(PosDbContext))
                        .ToList();
                    foreach (var d in efConfigs) services.Remove(d);

                    services.AddDbContext<PosDbContext>((provider, options) =>
                    {
                        options.UseSqlServer(_fixture.ConnectionString);
                        options.AddPosSaveChangesInterceptors(provider);
                    });
                });
            });

        _anonymousClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _anonymousClient.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<(HttpClient Client, User User)> CreateClientWithTokenAsync(bool isSuperAdmin, IEnumerable<string>? permissions = null)
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var generator = new JwtTokenGenerator(config, TimeProvider.System);

        User user;
        if (isSuperAdmin)
        {
            using var db = _fixture.CreateDbContext(null, isSuperAdmin: true);
            var superAdminRole = await db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);
            if (superAdminRole == null)
            {
                superAdminRole = Role.CreatePlatformRole(
                    Role.SuperAdminRoleId,
                    PlatformScope.TenantId,
                    "SuperAdmin",
                    "Super Admin",
                    new[] { Permissions.Platform.DeadLetterView, Permissions.Platform.DeadLetterReprocess },
                    TimeProvider.System);
                db.Roles.Add(superAdminRole);
                await db.SaveChangesAsync();
            }
            else
            {
                superAdminRole.AddPermission(Permissions.Platform.DeadLetterView);
                superAdminRole.AddPermission(Permissions.Platform.DeadLetterReprocess);
                await db.SaveChangesAsync();
            }

            var email = new Email($"superadmin_{Guid.NewGuid():N}"[..20] + "@platform.com");
            user = User.Create(email, new PasswordHash("hashedpass"), Role.SuperAdminRoleId, null, "Super", "Admin", TimeProvider.System);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        else
        {
            Guid tenantId = Guid.NewGuid();
            using var db = _fixture.CreateDbContext(tenantId, isSuperAdmin: false);
            var role = Role.Create(
                tenantId,
                $"TenantRole_{Guid.NewGuid():N}"[..16],
                "Tenant Role",
                permissions ?? new[] { Permissions.Products.View },
                TimeProvider.System);
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            var email = new Email($"tenantuser_{Guid.NewGuid():N}"[..20] + "@tenant.com");
            user = User.Create(email, new PasswordHash("hashedpass"), role.Id, tenantId, "Regular", "User", TimeProvider.System);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        string token = generator.GenerateToken(user);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user);
    }

    [Fact]
    public async Task DeadLetterEndpoints_WithoutToken_Returns401Unauthorized()
    {
        // 1. Act: GET sin token
        var getResponse = await _anonymousClient.GetAsync("/api/v1/platform/outbox/dead-letter");

        // Assert: 401
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);

        // 2. Act: POST reproceso sin token
        var postResponse = await _anonymousClient.PostAsJsonAsync("/api/v1/platform/outbox/dead-letter/reprocess",
            new ReprocessDeadLetterMessagesRequest(new[] { Guid.NewGuid() }));

        // Assert: 401
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
    }

    [Fact]
    public async Task DeadLetterEndpoints_WhenCalledByRegularTenantUser_Returns403Forbidden()
    {
        // Arrange
        var (tenantClient, _) = await CreateClientWithTokenAsync(isSuperAdmin: false);

        // 1. Act: GET con tenant normal
        var getResponse = await tenantClient.GetAsync("/api/v1/platform/outbox/dead-letter");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);

        // 2. Act: POST reproceso con tenant normal
        var postResponse = await tenantClient.PostAsJsonAsync("/api/v1/platform/outbox/dead-letter/reprocess",
            new ReprocessDeadLetterMessagesRequest(new[] { Guid.NewGuid() }));

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [Fact]
    public async Task DeadLetterEndpoints_WhenCalledBySuperAdmin_Returns200AndReprocessesMessagesEffectively()
    {
        // 1. Arrange: Sembrar 2 mensajes en estado dead-letter en SQL Server
        using var db = _fixture.CreateDbContext(null, isSuperAdmin: true);
        var now = TimeProvider.System.GetUtcNow();
        var msg1 = OutboxMessage.Create(Guid.NewGuid(), null, "TestIntegrationDeadLetterEvent1", "{\"data\":1}", now);
        msg1.MarkAsDeadLetter("Error crítico 1");
        var msg2 = OutboxMessage.Create(Guid.NewGuid(), null, "TestIntegrationDeadLetterEvent2", "{\"data\":2}", now);
        msg2.MarkAsDeadLetter("Error crítico 2");

        db.OutboxMessages.AddRange(msg1, msg2);
        await db.SaveChangesAsync();

        var (superAdminClient, _) = await CreateClientWithTokenAsync(isSuperAdmin: true);

        // 2. Act: Consultar mensajes dead-letter
        var getResponse = await superAdminClient.GetAsync("/api/v1/platform/outbox/dead-letter");

        // Assert GET: 200 OK y contiene los mensajes creados
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var messages = await getResponse.Content.ReadFromJsonAsync<IReadOnlyList<DeadLetterMessageResponse>>();
        Assert.NotNull(messages);
        Assert.Contains(messages, m => m.Id == msg1.Id);
        Assert.Contains(messages, m => m.Id == msg2.Id);

        // 3. Act: Reprocesar los mensajes
        var request = new ReprocessDeadLetterMessagesRequest(new[] { msg1.Id, msg2.Id });
        var postResponse = await superAdminClient.PostAsJsonAsync("/api/v1/platform/outbox/dead-letter/reprocess", request);

        // Assert POST: 200 OK con conteo 2
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var reprocessResult = await postResponse.Content.ReadFromJsonAsync<ReprocessDeadLetterMessagesResponse>();
        Assert.NotNull(reprocessResult);
        Assert.Equal(2, reprocessResult.ReprocessedCount);

        // 4. Assert SQL Server: Verificar directamente en base de datos que ya no están en dead-letter
        using var verifyDb = _fixture.CreateDbContext(null, isSuperAdmin: true);
        var reloadedMsg1 = await verifyDb.OutboxMessages.FindAsync(msg1.Id);
        var reloadedMsg2 = await verifyDb.OutboxMessages.FindAsync(msg2.Id);

        Assert.NotNull(reloadedMsg1);
        Assert.False(reloadedMsg1.IsDeadLetter);
        Assert.Equal(0, reloadedMsg1.RetryCount);

        Assert.NotNull(reloadedMsg2);
        Assert.False(reloadedMsg2.IsDeadLetter);
        Assert.Equal(0, reloadedMsg2.RetryCount);
    }

    [Fact]
    public async Task DeadLetterPagination_RespectsMaxPageSizeLimit()
    {
        // 1. Arrange: Sembrar 3 mensajes dead-letter
        using var db = _fixture.CreateDbContext(null, isSuperAdmin: true);
        var now = TimeProvider.System.GetUtcNow();
        var msgA = OutboxMessage.Create(Guid.NewGuid(), null, "PaginationDeadLetterA", "{}", now);
        msgA.MarkAsDeadLetter("ErrA");
        var msgB = OutboxMessage.Create(Guid.NewGuid(), null, "PaginationDeadLetterB", "{}", now);
        msgB.MarkAsDeadLetter("ErrB");
        var msgC = OutboxMessage.Create(Guid.NewGuid(), null, "PaginationDeadLetterC", "{}", now);
        msgC.MarkAsDeadLetter("ErrC");

        db.OutboxMessages.AddRange(msgA, msgB, msgC);
        await db.SaveChangesAsync();

        var (superAdminClient, _) = await CreateClientWithTokenAsync(isSuperAdmin: true);

        // 2. Act: Consultar con pageSize=2
        var limitedResponse = await superAdminClient.GetAsync("/api/v1/platform/outbox/dead-letter?pageSize=2");
        Assert.Equal(HttpStatusCode.OK, limitedResponse.StatusCode);
        var pageItems = await limitedResponse.Content.ReadFromJsonAsync<IReadOnlyList<DeadLetterMessageResponse>>();
        Assert.NotNull(pageItems);
        Assert.True(pageItems.Count <= 2, $"El tamaño de la página debe ser menor o igual a 2, pero fue {pageItems.Count}");

        // 3. Act: Consultar con pageSize=101 (supera el límite de PaginationRules.MaxPageSize = 100)
        var overLimitResponse = await superAdminClient.GetAsync("/api/v1/platform/outbox/dead-letter?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, overLimitResponse.StatusCode);

        var json = await overLimitResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("status", out var statusEl));
        Assert.Equal(400, statusEl.GetInt32());
    }
}
