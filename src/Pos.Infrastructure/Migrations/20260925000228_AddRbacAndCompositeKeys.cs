using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacAndCompositeKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            // Paso 1 (Expandir): Agregar RoleId como NULLABLE para no romper usuarios existentes
            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Roles_TenantId_Id",
                table: "Roles",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Id",
                table: "Roles",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            // Paso 2 (Migración de Datos SQL): Asignar a cada usuario existente su RoleId correspondiente
            migrationBuilder.Sql(@"
-- 1. Asegurar que exista el SuperAdminRole global si no estuviera creado
IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Id] = '00000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO [Roles] ([Id], [TenantId], [Name], [Description], [PermissionsJson])
    VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000000', N'SuperAdminRole', N'Super Admin Role', N'[]');
END;

-- 2. Asegurar que cada Tenant existente tenga sembrado al menos su rol TenantAdmin
INSERT INTO [Roles] ([Id], [TenantId], [Name], [Description], [PermissionsJson])
SELECT NEWID(), t.[Id], N'TenantAdmin', N'Administrador del Tenant', N'[]'
FROM [Tenants] t
WHERE NOT EXISTS (
    SELECT 1 FROM [Roles] r WHERE r.[TenantId] = t.[Id] AND r.[Name] = N'TenantAdmin'
);

-- 3. Mapear usuarios de plataforma (TenantId NULL o Role = 'SuperAdmin') al SuperAdminRole
UPDATE u
SET u.[RoleId] = '00000000-0000-0000-0000-000000000001'
FROM [Users] u
WHERE u.[TenantId] IS NULL OR u.[Role] = N'SuperAdmin';

-- 4. Mapear usuarios de tenant según su columna legacy 'Role' coincidente con Roles(TenantId, Name)
UPDATE u
SET u.[RoleId] = r.[Id]
FROM [Users] u
INNER JOIN [Roles] r ON r.[TenantId] = u.[TenantId] AND r.[Name] = u.[Role]
WHERE u.[TenantId] IS NOT NULL AND u.[RoleId] IS NULL;

-- 5. Fallback de seguridad: asignar TenantAdmin a cualquier usuario de tenant que no haya coincidido
UPDATE u
SET u.[RoleId] = r.[Id]
FROM [Users] u
INNER JOIN [Roles] r ON r.[TenantId] = u.[TenantId] AND r.[Name] = N'TenantAdmin'
WHERE u.[TenantId] IS NOT NULL AND u.[RoleId] IS NULL;
");

            // Paso 3 (Contraer): Alterar la columna RoleId a NOT NULL
            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_RoleId",
                table: "Users",
                columns: new[] { "TenantId", "RoleId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_TenantId_RoleId",
                table: "Users",
                columns: new[] { "TenantId", "RoleId" },
                principalTable: "Roles",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_PlatformRole",
                table: "Users",
                sql: "[TenantId] IS NOT NULL OR [RoleId] = '00000000-0000-0000-0000-000000000001'");

            // Eliminar la columna de string legacy 'Role' de Users
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_PlatformRole",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_TenantId_RoleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_RoleId",
                table: "Users");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Roles_TenantId_Id",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Id",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Name",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);
        }
    }
}
