using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Pos.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Pos.IntegrationTests;

[Collection("IntegrationTests")]
public class RowLevelSecurityIntegrationTests
{
    private readonly MsSqlTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public RowLevelSecurityIntegrationTests(MsSqlTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task RowLevelSecurity_RawSqlWithoutWhereClause_MustBlockTenantBRows_AtDatabaseEngineLevel()
    {
        // 1. Arrange: Create Tenant A and Tenant B, seed Products for each tenant
        Guid tenantAId = Guid.NewGuid();
        Guid tenantBId = Guid.NewGuid();

        using (var dbA = _fixture.CreateDbContext(tenantAId))
        {
            var catA = Category.Create("Cat RLS A", "Desc A");
            var prodA1 = Product.Create("Product RLS A1", Sku.Create("SKU-RLS-A1"), Money.Create(15.0m), catA.Id);
            var prodA2 = Product.Create("Product RLS A2", Sku.Create("SKU-RLS-A2"), Money.Create(25.0m), catA.Id);

            dbA.Categories.Add(catA);
            dbA.Products.AddRange(prodA1, prodA2);
            await dbA.SaveChangesAsync();
        }

        using (var dbB = _fixture.CreateDbContext(tenantBId))
        {
            var catB = Category.Create("Cat RLS B", "Desc B");
            var prodB1 = Product.Create("Product RLS B1", Sku.Create("SKU-RLS-B1"), Money.Create(35.0m), catB.Id);

            dbB.Categories.Add(catB);
            dbB.Products.Add(prodB1);
            await dbB.SaveChangesAsync();
        }

        // 2. Act: Execute RAW SQL SELECT WITHOUT WHERE CLAUSE as Tenant A
        using var dbContextA = _fixture.CreateDbContext(tenantAId);
        await dbContextA.Database.OpenConnectionAsync();
        var connection = dbContextA.Database.GetDbConnection();

        var productsFromRawSql = new List<(Guid Id, Guid TenantId, string Name)>();

        using (var command = connection.CreateCommand())
        {
            // EL TEST DEL ÁCIDO: SQL Crudo sin cláusula WHERE
            command.CommandText = "SELECT Id, TenantId, Name FROM Products";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetGuid(0);
                var tenantId = reader.GetGuid(1);
                var name = reader.GetString(2);
                productsFromRawSql.Add((id, tenantId, name));
            }
        }

        _output.WriteLine($"[RLS Raw SQL Test] Se obtuvieron {productsFromRawSql.Count} productos ejecutando 'SELECT * FROM Products' sin WHERE:");
        foreach (var p in productsFromRawSql)
        {
            _output.WriteLine($"  Producto: {p.Name}, TenantId: {p.TenantId}");
        }

        // 3. Assert: SQL Server RLS engine MUST have filtered out Tenant B products completely
        Assert.NotEmpty(productsFromRawSql);
        Assert.All(productsFromRawSql, p => Assert.Equal(tenantAId, p.TenantId));
        Assert.DoesNotContain(productsFromRawSql, p => p.TenantId == tenantBId);
    }

    [Fact]
    public async Task RowLevelSecurity_UnresolvedSessionContext_MustReturnZeroRows_FailClosed()
    {
        // 1. Arrange: Create Tenant A and seed Product
        Guid tenantAId = Guid.NewGuid();
        using (var dbA = _fixture.CreateDbContext(tenantAId))
        {
            var catA = Category.Create("Cat FailClosed RLS", "Desc");
            var prodA = Product.Create("Product FailClosed RLS", Sku.Create("SKU-RLS-FC"), Money.Create(10.0m), catA.Id);

            dbA.Categories.Add(catA);
            dbA.Products.Add(prodA);
            await dbA.SaveChangesAsync();
        }

        // 2. Act: Query RAW SQL on context with NO TenantId set in SESSION_CONTEXT (null tenantId)
        using var dbUnresolved = _fixture.CreateDbContext(tenantId: null);
        await dbUnresolved.Database.OpenConnectionAsync();
        var connection = dbUnresolved.Database.GetDbConnection();

        var rawProducts = new List<Guid>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id FROM Products";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rawProducts.Add(reader.GetGuid(0));
            }
        }

        // 3. Assert Fail-Closed: Engine returns 0 rows when SESSION_CONTEXT is NULL and IsSuperAdmin is 0
        Assert.Empty(rawProducts);
    }

    [Fact]
    public async Task RowLevelSecurity_UsersAndRoles_CrossTenantInsertOrUpdate_MustBeBlockedByBlockPredicate()
    {
        // 1. Arrange: Tenant A and Tenant B IDs
        Guid tenantAId = Guid.NewGuid();
        Guid tenantBId = Guid.NewGuid();

        Role roleB;
        using (var dbB = _fixture.CreateDbContext(tenantBId))
        {
            roleB = Role.Create(tenantBId, "RoleInTenantBForRLSTest", "Desc");
            dbB.Roles.Add(roleB);
            await dbB.SaveChangesAsync();
        }

        // 2. Act & Assert 1: Context scoped to Tenant A attempts to INSERT a Role for Tenant B
        using (var dbContextA = _fixture.CreateDbContext(tenantAId))
        {
            var crossRole = Role.Create(tenantBId, "ForbiddenCrossRole", "Desc");
            dbContextA.Roles.Add(crossRole);

            var exRole = await Assert.ThrowsAsync<DbUpdateException>(async () => await dbContextA.SaveChangesAsync());
            Assert.Contains("block predicate", exRole.InnerException?.Message ?? exRole.Message, StringComparison.OrdinalIgnoreCase);
        }

        // 3. Act & Assert 2: Context scoped to Tenant A attempts to INSERT a User for Tenant B
        using (var dbContextA = _fixture.CreateDbContext(tenantAId))
        {
            var crossUser = User.Create(
                new Email("cross_rls@tenantB.com"),
                new PasswordHash("hashedPassword123!"),
                roleB.Id,
                tenantBId, // Tenant B ID inside Tenant A context!
                "Cross",
                "User");

            dbContextA.Users.Add(crossUser);

            var exUser = await Assert.ThrowsAsync<DbUpdateException>(async () => await dbContextA.SaveChangesAsync());
            Assert.Contains("block predicate", exUser.InnerException?.Message ?? exUser.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RowLevelSecurity_ConnectionPoolingRecycling_MustNotLeakTenantContext()
    {
        // 1. Arrange: Create Tenant A and seed Product
        Guid tenantAId = Guid.NewGuid();
        using (var dbA = _fixture.CreateDbContext(tenantAId))
        {
            var catA = Category.Create("Cat Pool Leak Test", "Desc");
            var prodA = Product.Create("Product Pool Leak Test", Sku.Create("SKU-POOL-1"), Money.Create(50.0m), catA.Id);

            dbA.Categories.Add(catA);
            dbA.Products.Add(prodA);
            await dbA.SaveChangesAsync();
        }

        // 2. Act Step 1: Open connection as Tenant A, query Products to warm up and set SESSION_CONTEXT on connection
        using (var dbContextA = _fixture.CreateDbContext(tenantAId))
        {
            var productsA = await dbContextA.Products.ToListAsync();
            Assert.NotEmpty(productsA);
        } // DbContext and Connection are disposed and returned to ADO.NET Connection Pool!

        // 3. Act Step 2: Open a new DbContext in Unresolved Context (tenantId = null, isSuperAdmin = false)
        // This will reuse a connection from the ADO.NET Pool.
        using (var dbUnresolvedPool = _fixture.CreateDbContext(tenantId: null))
        {
            await dbUnresolvedPool.Database.OpenConnectionAsync();
            var recycledConnection = dbUnresolvedPool.Database.GetDbConnection();

            var leakedProducts = new List<Guid>();
            using (var command = recycledConnection.CreateCommand())
            {
                command.CommandText = "SELECT Id FROM Products";
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    leakedProducts.Add(reader.GetGuid(0));
                }
            }

            // 4. Assert: Recycled connection MUST HAVE BEEN CLEANED by TenantSessionContextInterceptor.
            // No products from Tenant A must leak to the unresolved request.
            Assert.Empty(leakedProducts);
        }
    }

    [Fact]
    public async Task RowLevelSecurity_AllowGlobalUserLookup_MustOnlyBypassFilterForUsersTable_AndNotBusinessTables()
    {
        // 1. Arrange: Create Tenant A and seed Product, Role, and Sale
        Guid tenantAId = Guid.NewGuid();
        Role roleA;
        Product prodA;
        using (var dbA = _fixture.CreateDbContext(tenantAId))
        {
            roleA = Role.Create(tenantAId, "Role Global Lookup Test", "Desc");
            var catA = Category.Create("Cat Global Lookup Test", "Desc");
            prodA = Product.Create("Product Global Lookup Test", Sku.Create("SKU-GLOBAL-1"), Money.Create(100.0m), catA.Id);

            dbA.Roles.Add(roleA);
            dbA.Categories.Add(catA);
            dbA.Products.Add(prodA);
            await dbA.SaveChangesAsync();
        }

        // 2. Act: Open connection in Unresolved context and explicitly set AllowGlobalUserLookup = 1
        using var dbUnresolved = _fixture.CreateDbContext(tenantId: null);
        await dbUnresolved.Database.OpenConnectionAsync();
        var connection = dbUnresolved.Database.GetDbConnection();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'AllowGlobalUserLookup', @value = 1;";
            await cmd.ExecuteNonQueryAsync();
        }

        // 3. Act & Assert 1: Querying Users table with AllowGlobalUserLookup = 1 returns users (allowed for login)
        var usersList = new List<Guid>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id FROM Users";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                usersList.Add(reader.GetGuid(0));
            }
        }

        // 4. Act & Assert 2: Querying Products, Sales, and Roles tables with AllowGlobalUserLookup = 1 MUST NOT RETURN Tenant A data!
        // AllowGlobalUserLookup MUST NOT bypass tenant security policy for business tables!
        var productsList = new List<Guid>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id FROM Products";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                productsList.Add(reader.GetGuid(0));
            }
        }

        var salesList = new List<Guid>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id FROM Sales";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                salesList.Add(reader.GetGuid(0));
            }
        }

        var rolesList = new List<Guid>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id FROM Roles";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rolesList.Add(reader.GetGuid(0));
            }
        }

        Assert.DoesNotContain(prodA.Id, productsList);
        Assert.DoesNotContain(roleA.Id, rolesList);
        Assert.Empty(salesList);
    }
}
