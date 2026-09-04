using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence.Context;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class UserPersistenceTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public async Task PersistUser_ShouldSaveArgon2idHashedPasswordAndRetrieveByEmail()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        string plainPassword = "EnterprisePassword2026!";
        string passwordHash = _hasher.HashPassword(plainPassword);

        var user = User.Create(
            email: "admin@enterprise.com",
            passwordHash: passwordHash,
            role: UserRole.SuperAdmin,
            tenantId: null,
            firstName: "Admin",
            lastName: "Enterprise"
        );

        // Act - Guardar en base de datos
        await using (var context = new PosDbContext(options))
        {
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        // Assert - Recuperar de la base de datos y verificar
        await using (var context = new PosDbContext(options))
        {
            var retrievedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);

            Assert.NotNull(retrievedUser);
            Assert.Equal("admin@enterprise.com", retrievedUser.Email.Value);
            Assert.Equal(UserRole.SuperAdmin, retrievedUser.Role);

            // Confirmar que el password hash NO quedó en texto plano
            Assert.NotEqual(plainPassword, retrievedUser.PasswordHash.Value);
            Assert.StartsWith("$argon2id$", retrievedUser.PasswordHash.Value);

            // Confirmar verificación exitosa con el passwordHasher
            bool isPasswordCorrect = _hasher.Verify(plainPassword, retrievedUser.PasswordHash);
            Assert.True(isPasswordCorrect);
        }
    }
}
