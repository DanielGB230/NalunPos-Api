using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Context;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class TenantSaveChangesInterceptorTests
{
    private sealed class TestTenantContext : ICurrentTenantContext
    {
        public Guid? TenantId { get; set; }
        public bool IsSuperAdmin { get; set; }
        public bool HasTenant => TenantId.HasValue && TenantId.Value != Guid.Empty;

        public Guid RequireTenantId()
        {
            if (!TenantId.HasValue || TenantId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("Tenant context is required.");
            }
            return TenantId.Value;
        }
    }

    [Fact]
    public async Task SaveChanges_WhenNoTenantAndAddedBranchHasEmptyTenantId_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantContext = new TestTenantContext { TenantId = null };
        var interceptor = new TenantSaveChangesInterceptor(tenantContext);

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new PosDbContext(options, tenantInterceptor: interceptor);

        // Branch no es IPlatformScopedEntity; su sobrecarga sin tenant genera TenantId = Guid.Empty
        var branch = Branch.Create("Sucursal Test", Address.Create("Calle 1", "Ciudad", "12345", "Peru"));

        await context.Branches.AddAsync(branch);

        // Act & Assert: lanza InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("No se puede persistir la entidad 'Branch'", ex.Message);
        Assert.Contains("sin un contexto de tenant activo", ex.Message);
    }

    [Fact]
    public async Task SaveChanges_WhenNoTenantAndAddedEntityIsPlatformScoped_Succeeds()
    {
        // Arrange
        var tenantContext = new TestTenantContext { TenantId = null };
        var interceptor = new TenantSaveChangesInterceptor(tenantContext);

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new PosDbContext(options, tenantInterceptor: interceptor);

        // Role y AuditLog son IPlatformScopedEntity y pueden existir con TenantId == Guid.Empty
        var role = Role.Create(Guid.Empty, "SuperAdminRoleTest", "Rol de plataforma");
        var auditLog = AuditLog.Create(Guid.Empty, "Tenants", "rec-1", AuditAction.Insert);

        await context.Roles.AddAsync(role);
        await context.AuditLogs.AddAsync(auditLog);

        // Act
        var savedCount = await context.SaveChangesAsync();

        // Assert: se persiste exitosamente
        Assert.True(savedCount >= 2);

        var persistedRole = await context.Roles.FindAsync(role.Id);
        Assert.NotNull(persistedRole);
        Assert.Equal(Guid.Empty, persistedRole.TenantId);

        var persistedAudit = await context.AuditLogs.FindAsync(auditLog.Id);
        Assert.NotNull(persistedAudit);
        Assert.Equal(Guid.Empty, persistedAudit.TenantId);
    }

    [Fact]
    public async Task SaveChanges_WhenTenantInContextAndEntityHasEmptyTenantId_AssignsTenantIdAutomatically()
    {
        // Arrange
        var contextTenantId = Guid.NewGuid();
        var tenantContext = new TestTenantContext { TenantId = contextTenantId };
        var interceptor = new TenantSaveChangesInterceptor(tenantContext);

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new PosDbContext(options, tenantInterceptor: interceptor);

        var branch = Branch.Create("Sucursal AutoTenant", Address.Create("Calle 2", "Ciudad", "12345", "Peru"));

        await context.Branches.AddAsync(branch);

        // Act
        var savedCount = await context.SaveChangesAsync();

        // Assert: asigna automáticamente el tenant del contexto
        Assert.True(savedCount > 0);
        var persistedBranch = await context.Branches.FindAsync(branch.Id);
        Assert.NotNull(persistedBranch);
        Assert.Equal(contextTenantId, persistedBranch.TenantId);
    }
}
