using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeRevisionsAndRateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncomeSourceRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncomeSourceId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeSourceRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeSourceRevisions_IncomeSources_IncomeSourceId",
                        column: x => x.IncomeSourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // One revision per existing source, from the month it started, with the values it has today:
            // past months stay as they were. Sources that already have revisions are skipped.
            migrationBuilder.Sql(@"
                INSERT INTO IncomeSourceRevisions
                    (IncomeSourceId, EffectiveFrom, Amount, Currency, Frequency, Note, CreatedAtUtc, UserId)
                SELECT s.Id, DATEFROMPARTS(YEAR(s.StartDate), MONTH(s.StartDate), 1),
                       s.Amount, s.Currency, s.Frequency, NULL, SYSUTCDATETIME(), s.UserId
                FROM IncomeSources s
                WHERE NOT EXISTS (SELECT 1 FROM IncomeSourceRevisions r WHERE r.IncomeSourceId = s.Id);");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "IncomeSources");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "IncomeSources");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "IncomeSources");

            // The date a rate was last saved becomes the date it applies from.
            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_UserId_Currency",
                table: "ExchangeRates");

            migrationBuilder.RenameColumn(
                name: "UpdatedOn",
                table: "ExchangeRates",
                newName: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_UserId_Currency_EffectiveFrom",
                table: "ExchangeRates",
                columns: new[] { "UserId", "Currency", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSourceRevisions_IncomeSourceId_EffectiveFrom",
                table: "IncomeSourceRevisions",
                columns: new[] { "IncomeSourceId", "EffectiveFrom" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "IncomeSources",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "IncomeSources",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                table: "IncomeSources",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Without history each source keeps its latest values and each currency its latest rate.
            migrationBuilder.Sql(@"
                UPDATE s SET s.Amount = r.Amount, s.Currency = r.Currency, s.Frequency = r.Frequency
                FROM IncomeSources s
                CROSS APPLY (SELECT TOP 1 * FROM IncomeSourceRevisions x
                             WHERE x.IncomeSourceId = s.Id ORDER BY x.EffectiveFrom DESC) r;");

            migrationBuilder.Sql(@"
                DELETE e FROM ExchangeRates e
                WHERE EXISTS (SELECT 1 FROM ExchangeRates n
                              WHERE n.UserId = e.UserId AND n.Currency = e.Currency AND n.EffectiveFrom > e.EffectiveFrom);");

            migrationBuilder.DropTable(
                name: "IncomeSourceRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_UserId_Currency_EffectiveFrom",
                table: "ExchangeRates");

            migrationBuilder.RenameColumn(
                name: "EffectiveFrom",
                table: "ExchangeRates",
                newName: "UpdatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_UserId_Currency",
                table: "ExchangeRates",
                columns: new[] { "UserId", "Currency" },
                unique: true);
        }
    }
}
