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
using Pos.Application.Authentication.Commands.Login;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.IntegrationTests.Fixtures;
using Xunit;

namespace Pos.IntegrationTests;

/// <summary>
/// Contrato HTTP de los filtros de rango de fechas (F1.F.1): todo StartDate/EndDate
/// debe ser un instante UTC con offset explícito; un valor ambiguo se rechaza con 400
/// ProblemDetails y un code estable, nunca con 500 ni con un filtro silenciosamente ignorado.
/// </summary>
[Collection("IntegrationTests")]
public class DateRangeFilterHttpIntegrationTests : IAsyncLifetime
{
    private const string OffsetRequiredCode = "DateRange.OffsetRequired";
    private const string StartDateWithOffset = "2026-02-01T00:00:00Z";
    private const string StartDateWithoutOffset = "2026-02-01T00:00:00";

    private static readonly DateTime EarlyInstant = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LateInstant = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly MsSqlTestFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private int _ipCounter;

    public DateRangeFilterHttpIntegrationTests(MsSqlTestFixture fixture)
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
                builder.UseSetting("SuperAdminSettings:Email", "superadmin_daterange@test.com");
                builder.UseSetting("SuperAdminSettings:Password", "SuperSecurePass123!");
                builder.UseSetting("SuperAdminSettings:FirstName", "Super");
                builder.UseSetting("SuperAdminSettings:LastName", "Admin");
                builder.UseSetting("JwtSettings:Secret", "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!");
                builder.UseSetting("JwtSettings:Issuer", "NalunPosApi");
                builder.UseSetting("JwtSettings:Audience", "NalunPosClients");
                builder.UseSetting("CorsSettings:AllowedOrigins:0", "http://localhost:4200");

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                        ["RateLimiting:AuthPolicy:PermitLimit"] = "100",
                        ["RateLimiting:SensitiveOperationsPolicy:PermitLimit"] = "100",
                        ["RateLimiting:GlobalApiPolicy:PermitLimit"] = "200"
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    var outboxDescriptor = services.FirstOrDefault(d =>
                        d.ImplementationType == typeof(Pos.Api.BackgroundServices.OutboxProcessorBackgroundService));
                    if (outboxDescriptor != null) services.Remove(outboxDescriptor);

                    var reconciliationDescriptor = services.FirstOrDefault(d =>
                        d.ImplementationType == typeof(Pos.Api.BackgroundServices.InvoiceReconciliationBackgroundService));
                    if (reconciliationDescriptor != null) services.Remove(reconciliationDescriptor);

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

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Sales_WithExplicitUtcOffset_Returns200AndFiltersByInstant()
    {
        var seeded = await ProvisionTenantWithDateRangeDataAsync();
        using var client = CreateClient(seeded.Token);

        var response = await client.GetAsync(
            $"/api/v1/sales?startDate={Uri.EscapeDataString(StartDateWithOffset)}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());
        var items = document.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(seeded.LateSaleId, items[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Sales_WithoutExplicitUtcOffset_Returns400ProblemDetailsWithStableCode()
    {
        var seeded = await ProvisionTenantWithDateRangeDataAsync();
        using var client = CreateClient(seeded.Token);

        var response = await client.GetAsync(
            $"/api/v1/sales?startDate={Uri.EscapeDataString(StartDateWithoutOffset)}");

        await AssertOffsetRequiredProblemAsync(response);
    }

    [Fact]
    public async Task WarehouseMovements_WithExplicitUtcOffset_Returns200AndFiltersByInstant()
    {
        var seeded = await ProvisionTenantWithDateRangeDataAsync();
        using var client = CreateClient(seeded.Token);

        var response = await client.GetAsync(
            $"/api/v1/warehouses/{seeded.WarehouseId}/movements?startDate={Uri.EscapeDataString(StartDateWithOffset)}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());
        var items = document.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(seeded.LateMovementId, items[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task WarehouseMovements_WithoutExplicitUtcOffset_Returns400ProblemDetailsWithStableCode()
    {
        var seeded = await ProvisionTenantWithDateRangeDataAsync();
        using var client = CreateClient(seeded.Token);

        var response = await client.GetAsync(
            $"/api/v1/warehouses/{seeded.WarehouseId}/movements?startDate={Uri.EscapeDataString(StartDateWithoutOffset)}");

        await AssertOffsetRequiredProblemAsync(response);
    }

    [Fact]
    public async Task InventoryHistory_WithExplicitUtcOffset_Returns200AndFiltersByInstant()
    {
        var seeded = await ProvisionTenantWithDateRangeDataAsync();
        using var client = CreateClient(seeded.Token);

        var response = await client.GetAsync(
            $"/api/v1/inventory/products/{seeded.ProductId}/movements?startDate={Uri.EscapeDataString(StartDateWithOffset)}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());
        var items = document.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(seeded.LateMovementId, items[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task InventoryHistory_WithoutExplicitUtcOffset_Returns400ProblemDetailsWithStableCode()
    {
        var seeded = await ProvisionTenantWithDateRangeDataAsync();
        using var client = CreateClient(seeded.Token);

        var response = await client.GetAsync(
            $"/api/v1/inventory/products/{seeded.ProductId}/movements?startDate={Uri.EscapeDataString(StartDateWithoutOffset)}");

        await AssertOffsetRequiredProblemAsync(response);
    }

    private static async Task AssertOffsetRequiredProblemAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.True(
            document.RootElement.TryGetProperty("code", out var code),
            $"Se esperaba la propiedad 'code' en ProblemDetails: {body}");
        Assert.Equal(OffsetRequiredCode, code.GetString());
    }

    private HttpClient CreateClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        int ipNum = Interlocked.Increment(ref _ipCounter);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.230.{(ipNum >> 8) & 0xFF}.{ipNum & 0xFF}");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private async Task<LoginResponse> CreateAndLoginSuperAdminAsync()
    {
        string unique = Guid.NewGuid().ToString("N")[..10];
        string superEmail = $"sa_daterange_{unique}@platform.test";
        const string superPassword = "SuperSqlPassword123!";

        await using (var dbSuper = _fixture.CreateDbContext(tenantId: null, isSuperAdmin: true))
        {
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var superRole = await dbSuper.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == Role.SuperAdminRoleId);
            if (superRole == null)
            {
                superRole = Role.CreatePlatformRole(
                    Role.SuperAdminRoleId,
                    PlatformScope.TenantId,
                    PlatformRoles.SuperAdmin,
                    "Administrador Global",
                    SuperAdminRoleCatalog.Permissions,
                    TimeProvider.System);
                dbSuper.Roles.Add(superRole);
            }
            else
            {
                superRole.SyncPermissions(SuperAdminRoleCatalog.Permissions);
            }

            var superUser = User.Create(
                new Email(superEmail),
                new PasswordHash(hasher.HashPassword(superPassword)),
                Role.SuperAdminRoleId,
                tenantId: null,
                "Super",
                "SqlAdmin",
                TimeProvider.System);
            dbSuper.Users.Add(superUser);
            await dbSuper.SaveChangesAsync();
        }

        using var anonymousClient = CreateClient();
        var saLoginResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(superEmail, superPassword));

        Assert.Equal(HttpStatusCode.OK, saLoginResponse.StatusCode);
        var saPayload = await saLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(saPayload);
        return saPayload;
    }

    private async Task<SeededTenant> ProvisionTenantWithDateRangeDataAsync()
    {
        var saPayload = await CreateAndLoginSuperAdminAsync();
        string unique = Guid.NewGuid().ToString("N")[..10];
        string tenantAdminEmail = $"admin_daterange_{unique}@empresa.test";
        const string tenantPassword = "TenantPassword123!";

        using var superAdminClient = CreateClient(saPayload.Token);
        var createTenantResponse = await superAdminClient.PostAsJsonAsync(
            "/api/v1/platform/tenants",
            new CreateTenantRequest($"Empresa DateRange {unique}", $"TAX-DR-{unique}", tenantAdminEmail, tenantPassword));

        Assert.Equal(HttpStatusCode.Created, createTenantResponse.StatusCode);
        Guid tenantId = await createTenantResponse.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, tenantId);

        // Activar el tenant antes de permitir login
        var activateResp = await superAdminClient.PatchAsJsonAsync(
            $"/api/v1/platform/tenants/{tenantId}/status",
            new ChangeStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, activateResp.StatusCode);

        using var anonymousClient = CreateClient();
        var loginResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(tenantAdminEmail, tenantPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginPayload);
        Assert.Equal(tenantId, loginPayload.TenantId);

        Guid warehouseId;
        Guid productId;
        Guid earlySaleId;
        Guid lateSaleId;
        Guid earlyMovementId;
        Guid lateMovementId;

        await using (var db = _fixture.CreateDbContext(tenantId))
        {
            warehouseId = (await db.Warehouses.FirstAsync()).Id;

            var category = Category.Create(tenantId, $"Cat DateRange {unique}", "Categoría de prueba", TimeProvider.System);
            var product = Product.Create(
                tenantId,
                $"Producto DateRange {unique}",
                Sku.Create($"SKU-{unique}"),
                Money.Create(10m, "USD"),
                category.Id,
                TimeProvider.System);
            db.Categories.Add(category);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;

            var earlySale = Sale.Create(
                tenantId,
                $"RC-EARLY-{unique}",
                Guid.NewGuid(),
                customerId: null,
                [SaleLineItem.Create(tenantId, product.Id, product.Name, 1m, Money.Create(10m, "USD"))],
                new FixedTimeProvider(new DateTimeOffset(EarlyInstant)));
            var lateSale = Sale.Create(
                tenantId,
                $"RC-LATE-{unique}",
                Guid.NewGuid(),
                customerId: null,
                [SaleLineItem.Create(tenantId, product.Id, product.Name, 1m, Money.Create(10m, "USD"))],
                new FixedTimeProvider(new DateTimeOffset(LateInstant)));

            var earlyMovement = InventoryMovement.Record(
                product.Id,
                warehouseId,
                5m,
                InventoryMovementType.Purchase,
                referenceId: null,
                notes: "seed early",
                containerId: null,
                batchNumber: null,
                expirationDate: null,
                occurredAtUtc: EarlyInstant,
                TimeProvider.System);
            var lateMovement = InventoryMovement.Record(
                product.Id,
                warehouseId,
                5m,
                InventoryMovementType.Purchase,
                referenceId: null,
                notes: "seed late",
                containerId: null,
                batchNumber: null,
                expirationDate: null,
                occurredAtUtc: LateInstant,
                TimeProvider.System);

            db.Sales.AddRange(earlySale, lateSale);
            db.InventoryMovements.AddRange(earlyMovement, lateMovement);
            await db.SaveChangesAsync();

            earlySaleId = earlySale.Id;
            lateSaleId = lateSale.Id;
            earlyMovementId = earlyMovement.Id;
            lateMovementId = lateMovement.Id;
        }

        return new SeededTenant(
            loginPayload.Token,
            warehouseId,
            productId,
            earlySaleId,
            lateSaleId,
            earlyMovementId,
            lateMovementId);
    }

    private sealed record SeededTenant(
        string Token,
        Guid WarehouseId,
        Guid ProductId,
        Guid EarlySaleId,
        Guid LateSaleId,
        Guid EarlyMovementId,
        Guid LateMovementId);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
