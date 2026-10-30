using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTripFuelPricePair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Trip_FuelPricePerLiter_Pair",
                table: "Trips",
                sql: "([FuelPricePerLiterAmount] IS NULL AND [FuelPricePerLiterCurrency] IS NULL) OR ([FuelPricePerLiterAmount] IS NOT NULL AND [FuelPricePerLiterCurrency] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trip_FuelPricePerLiter_Pair",
                table: "Trips");
        }
    }
}
