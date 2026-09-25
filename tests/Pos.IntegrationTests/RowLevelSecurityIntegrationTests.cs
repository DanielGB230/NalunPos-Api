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
}
