using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxLockFields : Migration
    {
        private static readonly string[] LockIndexColumns = ["ProcessedOnUtc", "IsDeadLetter", "LockedUntilUtc", "NextRetryAtUtc"];
        private static readonly string[] PreviousRetryIndexColumns = ["ProcessedOnUtc", "IsDeadLetter", "NextRetryAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_IsDeadLetter_NextRetryAtUtc",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "OutboxMessages",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntilUtc",
                table: "OutboxMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_IsDeadLetter_LockedUntilUtc_NextRetryAtUtc",
                table: "OutboxMessages",
                columns: LockIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_IsDeadLetter_LockedUntilUtc_NextRetryAtUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_IsDeadLetter_NextRetryAtUtc",
                table: "OutboxMessages",
                columns: PreviousRetryIndexColumns);
        }
    }
}
