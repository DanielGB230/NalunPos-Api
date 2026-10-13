using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Application.Warehouses.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;
using Pos.IntegrationTests.Fixtures;
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
        var sp = _fixture.CreateServiceProvider(tenantId: null, isSuperAdmin: true);
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        // Asegurar que el rol de SuperAdmin en BD cuente con el permiso del comando para evaluar específicamente la barrera de tenant
        var superAdminRole = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.Id == Role.SuperAdminRoleId);
        superAdminRole.AddPermission(Pos.Application.Common.Authorization.Permissions.Warehouses.Create);
        await db.SaveChangesAsync();

        int warehousesCountBefore = await db.Warehouses.IgnoreQueryFilters().CountAsync();

        // Creamos comando [RequiresTenant]
        var command = new CreateWarehouseCommand(
            BranchId: Guid.NewGuid(),
            Name: "Almacen No Autorizado SuperAdmin",
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
            .FirstOrDefaultAsync(w => w.Name == "Almacen No Autorizado SuperAdmin");
        Assert.Null(createdWarehouse);
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
