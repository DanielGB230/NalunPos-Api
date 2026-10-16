using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignCashRegistersBranchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Saneamiento defensivo en desarrollo: Asignar BranchId de una sucursal existente del mismo tenant
            migrationBuilder.Sql(@"
                UPDATE cr
                SET cr.BranchId = (
                    SELECT TOP 1 b.Id 
                    FROM Branches b 
                    WHERE b.TenantId = cr.TenantId
                )
                FROM CashRegisters cr
                WHERE cr.BranchId = '00000000-0000-0000-0000-000000000000'
                  AND EXISTS (SELECT 1 FROM Branches b WHERE b.TenantId = cr.TenantId);
            ");

            // Si existen cajas huérfanas en un tenant sin ninguna sucursal registrada y sin sesiones asociadas, eliminarlas para sanear
            migrationBuilder.Sql(@"
                DELETE FROM CashRegisters
                WHERE BranchId = '00000000-0000-0000-0000-000000000000'
                  AND Id NOT IN (SELECT DISTINCT CashRegisterId FROM CashRegisterSessions);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_BranchId",
                table: "CashRegisters",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_TenantId_BranchId",
                table: "CashRegisters",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashRegisters_Branches_BranchId",
                table: "CashRegisters",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Restricción a nivel de base de datos: jamás permitir Guid.Empty en BranchId
            migrationBuilder.Sql(@"
                ALTER TABLE [dbo].[CashRegisters] 
                ADD CONSTRAINT [CK_CashRegisters_BranchId_NotEmpty] 
                CHECK ([BranchId] <> '00000000-0000-0000-0000-000000000000');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE [dbo].[CashRegisters] 
                DROP CONSTRAINT IF EXISTS [CK_CashRegisters_BranchId_NotEmpty];
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_CashRegisters_Branches_BranchId",
                table: "CashRegisters");

            migrationBuilder.DropIndex(
                name: "IX_CashRegisters_BranchId",
                table: "CashRegisters");

            migrationBuilder.DropIndex(
                name: "IX_CashRegisters_TenantId_BranchId",
                table: "CashRegisters");
        }
    }
}
