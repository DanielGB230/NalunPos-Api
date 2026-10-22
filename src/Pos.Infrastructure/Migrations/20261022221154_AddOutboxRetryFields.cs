using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryFields : Migration
    {
        private static readonly string[] RetryIndexColumns = ["ProcessedOnUtc", "IsDeadLetter", "NextRetryAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeadLetter",
                table: "OutboxMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAtUtc",
                table: "OutboxMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_IsDeadLetter_NextRetryAtUtc",
                table: "OutboxMessages",
                columns: RetryIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_IsDeadLetter_NextRetryAtUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "IsDeadLetter",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextRetryAtUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "OutboxMessages");
        }
    }
}
