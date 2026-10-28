using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCamperManualSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManualMeasurementsJson",
                table: "Campers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualSourceName",
                table: "Campers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualSourceUrl",
                table: "Campers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualMeasurementsJson",
                table: "Campers");

            migrationBuilder.DropColumn(
                name: "ManualSourceName",
                table: "Campers");

            migrationBuilder.DropColumn(
                name: "ManualSourceUrl",
                table: "Campers");
        }
    }
}
