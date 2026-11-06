using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedPlaceRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SavedPlaces",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SavedPlaces");
        }
    }
}
