using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenFamilyExpiryAndPurgeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FamilyExpiresAtUtc",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: true);

            // Backfill: las familias existentes conservan como vigencia absoluta la
            // expiración que ya tenían sus tokens. No se recalcula ni se pierde ninguna fila.
            migrationBuilder.Sql("""
                UPDATE [RefreshTokens]
                SET [FamilyExpiresAtUtc] = [ExpiresAtUtc]
                WHERE [FamilyExpiresAtUtc] IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FamilyExpiresAtUtc",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_RevokedAtUtc",
                table: "RefreshTokens",
                column: "RevokedAtUtc",
                filter: "[RevokedAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_RevokedAtUtc",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "FamilyExpiresAtUtc",
                table: "RefreshTokens");
        }
    }
}
