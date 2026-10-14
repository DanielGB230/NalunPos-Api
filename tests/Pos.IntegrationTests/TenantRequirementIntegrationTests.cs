using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Warehouses.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.IntegrationTests.Fixtures;
using Pos.IntegrationTests.Support;
using Xunit;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class TenantRequirementIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;

    public TenantRequirementIntegrationTests(MsSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SuperAdminWithoutTenant_ExecutingRequiresTenantCommand_ReturnsTenantRequiredErrorAndWritesNothingToDb()
    {
        // 1. Arrange: SuperAdmin context (sin tenant: TenantId = null, IsSuperAdmin = true)
        // Usamos FakeCurrentUserPermissions inyectado en el ServiceProvider del test para no mutar el rol SuperAdmin en BD
        var fakePermissions = new FakeCurrentUserPermissions()
            .Grant(Permissions.Warehouses.Create);

        var sp = _fixture.CreateServiceProvider(
            tenantId: null,
            isSuperAdmin: true,
            configureServices: services =>
            {
                services.AddScoped<ICurrentUserPermissions>(_ => fakePermissions);
            });

        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        string uniqueWarehouseName = $"Almacen No Autorizado SuperAdmin {Guid.NewGuid():N}";
        int warehousesCountBefore = await db.Warehouses.IgnoreQueryFilters().CountAsync();

        // Creamos comando [RequiresTenant]
        var command = new CreateWarehouseCommand(
            BranchId: Guid.NewGuid(),
            Name: uniqueWarehouseName,
            Description: "Intento de escribir entidad de tenant sin contexto de tenant",
            IsDefault: false);

        // 2. Act: Ejecutar a través del pipeline CQRS
        var result = await dispatcher.SendAsync(command);

        // 3. Assert: Retorna fallo con error tenant.required
        Assert.True(result.IsFailure);
        Assert.Equal("tenant.required", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);

        // Verificar que NO se escribió nada en la base de datos ni bajo tenants inventados
        int warehousesCountAfter = await db.Warehouses.IgnoreQueryFilters().CountAsync();
        Assert.Equal(warehousesCountBefore, warehousesCountAfter);

        var createdWarehouse = await db.Warehouses.IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Name == uniqueWarehouseName);
        Assert.Null(createdWarehouse);
    }

    [Fact]
    public async Task SuperAdminWithoutTenant_ExecutingCreateBranchCommand_ReturnsTenantRequiredErrorAndWritesNothingToDb()
    {
        // 1. Arrange: SuperAdmin context sin tenant ejecutando CreateBranchCommand (clasificado con [RequiresTenant] en Paso 9.1)
        var fakePermissions = new FakeCurrentUserPermissions()
            .Grant(Permissions.Branches.Create);

        var sp = _fixture.CreateServiceProvider(
            tenantId: null,
            isSuperAdmin: true,
            configureServices: services =>
            {
                services.AddScoped<ICurrentUserPermissions>(_ => fakePermissions);
            });

        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        string uniqueBranchName = $"Sucursal No Autorizada SuperAdmin {Guid.NewGuid():N}";
        int branchesCountBefore = await db.Branches.IgnoreQueryFilters().CountAsync();

        var command = new Pos.Application.Branches.Commands.CreateBranchCommand(
            Name: uniqueBranchName,
            Street: "Av. Central 456",
            City: "Lima",
            ZipCode: "15001",
            Country: "PE",
            PhoneNumber: "+51987654321");

        // 2. Act
        var result = await dispatcher.SendAsync(command);

        // 3. Assert: default-deny bloquea por tenant.required y nada se persiste en BD
        Assert.True(result.IsFailure);
        Assert.Equal("tenant.required", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);

        int branchesCountAfter = await db.Branches.IgnoreQueryFilters().CountAsync();
        Assert.Equal(branchesCountBefore, branchesCountAfter);

        var createdBranch = await db.Branches.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Name == uniqueBranchName);
        Assert.Null(createdBranch);
    }

    [Fact]
    public async Task PlatformOperationWithoutTenant_GeneratesAuditLogWithPlatformScopeTenantIdGuidEmpty()
    {
        // 1. Arrange: SuperAdmin ejecuta una operación de plataforma (CreateTenantCommand con [AllowsNoTenant])
        var fakePermissions = new FakeCurrentUserPermissions()
            .Grant(Permissions.Tenants.Create);

        var sp = _fixture.CreateServiceProvider(
            tenantId: null,
            isSuperAdmin: true,
            configureServices: services =>
            {
                services.AddScoped<ICurrentUserPermissions>(_ => fakePermissions);
            });

        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        string uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var command = new Pos.Application.Platform.Tenants.Commands.CreateTenant.CreateTenantCommand(
            Name: $"Tenant Audit Test {uniqueSuffix}",
            DocumentNumber: $"209{DateTime.UtcNow:HHmmssff}",
            AdminEmail: $"admin_audit_{uniqueSuffix}@enterprise.test",
            AdminPassword: "Password123!");

        // 2. Act
        var result = await dispatcher.SendAsync(command);

        // 3. Assert
        Assert.True(result.IsSuccess);
        Guid createdTenantId = result.Value;

        // Verificar que la auditoría registró la operación con TenantId == Guid.Empty para el nuevo tenant
        var tenantAuditLog = await db.AuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.TableName == "Tenants" && a.RecordId == createdTenantId.ToString());

        Assert.NotNull(tenantAuditLog);
        Assert.Equal(Guid.Empty, tenantAuditLog.TenantId);
    }

    [Fact]
    public async Task TenantAdminWithTenant_ExecutingRequiresTenantCommand_SucceedsAndPersistsWithCorrectTenantId()
    {
        // 1. Arrange: Crear tenant real con sucursal y contexto de TenantAdmin
        Guid validTenantId = Guid.NewGuid();

        // Sembrar Tenant y Branch en BD para este tenant
        using (var seedDb = _fixture.CreateDbContext(validTenantId))
        {
            var tenant = Tenant.Create(
                $"Tenant Corp {validTenantId.ToString()[..8]}",
                $"208{DateTime.UtcNow:HHmmssff}");
            typeof(Tenant).GetProperty("Id")!.SetValue(tenant, validTenantId);

            var address = Address.Create("Av. Principal 123", "Lima", "15001", "PE");
            var branch = Branch.Create(validTenantId, "Sucursal Central", address, "+51999999999");

            seedDb.Tenants.Add(tenant);
            seedDb.Branches.Add(branch);
            await seedDb.SaveChangesAsync();
        }

        var sp = _fixture.CreateServiceProvider(tenantId: validTenantId, isSuperAdmin: false);
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var existingBranch = await db.Branches.FirstAsync(b => b.TenantId == validTenantId);

        var command = new CreateWarehouseCommand(
            BranchId: existingBranch.Id,
            Name: $"Almacen Principal {validTenantId.ToString()[..8]}",
            Description: "Almacen creado legítimamente por TenantAdmin",
            IsDefault: false);

        // 2. Act
        var result = await dispatcher.SendAsync(command);

        // 3. Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // Verificar en BD que se persistió exactamente bajo validTenantId y no bajo Guid.Empty ni Guid inventado
        var persistedWarehouse = await db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == result.Value.Id);

        Assert.NotNull(persistedWarehouse);
        Assert.Equal(validTenantId, persistedWarehouse.TenantId);
        Assert.NotEqual(Guid.Empty, persistedWarehouse.TenantId);
    }
}
