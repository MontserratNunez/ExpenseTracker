using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AlertThresholdPercent",
                table: "BudgetPlans",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 15m);

            migrationBuilder.AddColumn<int>(
                name: "SavingsIncomeSourceId",
                table: "BudgetPlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavingsType",
                table: "BudgetPlans",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "FixedAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "SavingsValue",
                table: "BudgetPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // The allocation of the savings category becomes the reserve of its plan. With several
            // savings categories, those of the same type and source are added up; the others stay.
            migrationBuilder.Sql(@"
                WITH s AS (
                    SELECT a.Id, a.BudgetPlanId, a.AllocationType, a.IncomeSourceId, a.Value,
                           ROW_NUMBER() OVER (PARTITION BY a.BudgetPlanId ORDER BY
                               CASE a.AllocationType WHEN 'PercentageOfIncome' THEN 0 ELSE 1 END, a.Value DESC, a.Id) AS rn
                    FROM BudgetAllocations a
                    INNER JOIN Categories c ON c.Id = a.CategoryId
                    WHERE c.IsSavings = 1)
                SELECT s.Id, s.BudgetPlanId, s.AllocationType, s.IncomeSourceId, s.Value
                INTO #Reserve
                FROM s
                INNER JOIN s AS pick ON pick.BudgetPlanId = s.BudgetPlanId AND pick.rn = 1
                    AND pick.AllocationType = s.AllocationType
                    AND ISNULL(pick.IncomeSourceId, 0) = ISNULL(s.IncomeSourceId, 0);

                UPDATE p SET
                    p.SavingsType = r.AllocationType,
                    p.SavingsIncomeSourceId = r.IncomeSourceId,
                    p.SavingsValue = CASE WHEN r.AllocationType = 'PercentageOfIncome' AND r.Value > 100 THEN 100 ELSE r.Value END
                FROM BudgetPlans p
                INNER JOIN (SELECT BudgetPlanId, MAX(AllocationType) AS AllocationType,
                                   MAX(IncomeSourceId) AS IncomeSourceId, SUM(Value) AS Value
                            FROM #Reserve GROUP BY BudgetPlanId) r ON r.BudgetPlanId = p.Id;

                DELETE FROM BudgetAllocations WHERE Id IN (SELECT Id FROM #Reserve);
                DROP TABLE #Reserve;");

            // Savings is no longer an expense category. Unused ones are removed. The ones with history are
            // deactivated and remembered: phase 25 turns their expenses into transfers.
            migrationBuilder.Sql(@"
                CREATE TABLE LegacySavingsCategories (
                    CategoryId int NOT NULL PRIMARY KEY,
                    UserId nvarchar(450) NOT NULL);");

            migrationBuilder.Sql(@"
                DELETE c FROM Categories c
                WHERE c.IsSavings = 1
                  AND NOT EXISTS (SELECT 1 FROM Expenses e WHERE e.CategoryId = c.Id)
                  AND NOT EXISTS (SELECT 1 FROM Goals g WHERE g.CategoryId = c.Id)
                  AND NOT EXISTS (SELECT 1 FROM BudgetAllocations a WHERE a.CategoryId = c.Id);

                INSERT INTO LegacySavingsCategories (CategoryId, UserId)
                SELECT Id, UserId FROM Categories WHERE IsSavings = 1;

                UPDATE Categories SET IsActive = 0 WHERE IsSavings = 1;");

            migrationBuilder.DropColumn(
                name: "IsSavings",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPlans_SavingsIncomeSourceId",
                table: "BudgetPlans",
                column: "SavingsIncomeSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetPlans_IncomeSources_SavingsIncomeSourceId",
                table: "BudgetPlans",
                column: "SavingsIncomeSourceId",
                principalTable: "IncomeSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSavings",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The remembered categories become savings again, and each reserve goes back to one of them.
            // Categories that were removed because they were unused are not recreated.
            migrationBuilder.Sql(@"
                UPDATE c SET c.IsSavings = 1, c.IsActive = 1
                FROM Categories c INNER JOIN LegacySavingsCategories l ON l.CategoryId = c.Id;

                INSERT INTO BudgetAllocations (BudgetPlanId, CategoryId, AllocationType, Value, IncomeSourceId, UserId, CreatedAtUtc)
                SELECT p.Id, l.CategoryId, p.SavingsType, p.SavingsValue, p.SavingsIncomeSourceId, p.UserId, SYSUTCDATETIME()
                FROM BudgetPlans p
                CROSS APPLY (SELECT TOP 1 x.CategoryId FROM LegacySavingsCategories x
                             INNER JOIN Categories c ON c.Id = x.CategoryId
                             WHERE x.UserId = p.UserId ORDER BY x.CategoryId) l
                WHERE p.SavingsValue > 0
                  AND NOT EXISTS (SELECT 1 FROM BudgetAllocations a WHERE a.BudgetPlanId = p.Id AND a.CategoryId = l.CategoryId);

                DROP TABLE LegacySavingsCategories;");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetPlans_IncomeSources_SavingsIncomeSourceId",
                table: "BudgetPlans");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPlans_SavingsIncomeSourceId",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "AlertThresholdPercent",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "SavingsIncomeSourceId",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "SavingsType",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "SavingsValue",
                table: "BudgetPlans");
        }
    }
}
