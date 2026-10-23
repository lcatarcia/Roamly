using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripFuelPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FuelPricePerLiterAmount",
                table: "Trips",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FuelPricePerLiterCurrency",
                table: "Trips",
                type: "char(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trip_FuelPricePerLiter_Amount",
                table: "Trips",
                sql: "[FuelPricePerLiterAmount] IS NULL OR [FuelPricePerLiterAmount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trip_FuelPricePerLiter_Currency",
                table: "Trips",
                sql: "[FuelPricePerLiterCurrency] IS NULL OR ([FuelPricePerLiterCurrency] = UPPER([FuelPricePerLiterCurrency]) AND LEN([FuelPricePerLiterCurrency]) = 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trip_FuelPricePerLiter_Amount",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trip_FuelPricePerLiter_Currency",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FuelPricePerLiterAmount",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FuelPricePerLiterCurrency",
                table: "Trips");
        }
    }
}
