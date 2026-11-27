using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPlans", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "BudgetPlanId",
                table: "BudgetAllocations",
                type: "int",
                nullable: true);

            // Existing allocations become each user's "Plan inicial", in force from the month of the
            // oldest one (so earlier months show "Sin plan") and without end.
            migrationBuilder.Sql(@"
                INSERT INTO BudgetPlans (Name, EffectiveFrom, EffectiveTo, CreatedAtUtc, UserId)
                SELECT N'Plan inicial',
                       DATEFROMPARTS(YEAR(MIN(CreatedAtUtc)), MONTH(MIN(CreatedAtUtc)), 1),
                       NULL, SYSUTCDATETIME(), UserId
                FROM BudgetAllocations
                GROUP BY UserId;");

            migrationBuilder.Sql(@"
                UPDATE a SET a.BudgetPlanId = p.Id
                FROM BudgetAllocations a
                INNER JOIN BudgetPlans p ON p.UserId = a.UserId;");

            migrationBuilder.DropIndex(
                name: "IX_BudgetAllocations_UserId_CategoryId",
                table: "BudgetAllocations");

            migrationBuilder.AlterColumn<int>(
                name: "BudgetPlanId",
                table: "BudgetAllocations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAllocations_BudgetPlanId_CategoryId",
                table: "BudgetAllocations",
                columns: new[] { "BudgetPlanId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPlans_UserId_EffectiveFrom",
                table: "BudgetPlans",
                columns: new[] { "UserId", "EffectiveFrom" });

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetAllocations_BudgetPlans_BudgetPlanId",
                table: "BudgetAllocations",
                column: "BudgetPlanId",
                principalTable: "BudgetPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Without plans there is one allocation per category: keep the one of the latest plan.
            migrationBuilder.Sql(@"
                DELETE a FROM BudgetAllocations a
                INNER JOIN BudgetPlans p ON p.Id = a.BudgetPlanId
                WHERE p.Id <> (SELECT TOP 1 l.Id FROM BudgetPlans l
                               WHERE l.UserId = p.UserId ORDER BY l.EffectiveFrom DESC);");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetAllocations_BudgetPlans_BudgetPlanId",
                table: "BudgetAllocations");

            migrationBuilder.DropTable(
                name: "BudgetPlans");

            migrationBuilder.DropIndex(
                name: "IX_BudgetAllocations_BudgetPlanId_CategoryId",
                table: "BudgetAllocations");

            migrationBuilder.DropColumn(
                name: "BudgetPlanId",
                table: "BudgetAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAllocations_UserId_CategoryId",
                table: "BudgetAllocations",
                columns: new[] { "UserId", "CategoryId" },
                unique: true);
        }
    }
}
