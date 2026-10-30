using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripRouteEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedDistanceKm",
                table: "Trips",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDurationMinutes",
                table: "Trips",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedDistanceKm",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EstimatedDurationMinutes",
                table: "Trips");
        }
    }
}
