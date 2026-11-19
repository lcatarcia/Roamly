using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedPlaceIsVisited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVisited",
                table: "SavedPlaces",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVisited",
                table: "SavedPlaces");
        }
    }
}
