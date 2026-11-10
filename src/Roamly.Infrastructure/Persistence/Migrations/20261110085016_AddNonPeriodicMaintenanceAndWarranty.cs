using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNonPeriodicMaintenanceAndWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceItem_Recurrence",
                table: "MaintenanceItems");

            migrationBuilder.AddColumn<bool>(
                name: "IsUnderWarranty",
                table: "MaintenanceLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LastServiceIsUnderWarranty",
                table: "MaintenanceItems",
                type: "bit",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceItem_Recurrence",
                table: "MaintenanceItems",
                sql: "[IntervalKm] IS NOT NULL OR [IntervalMonths] IS NOT NULL OR [LastServiceOnUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceItem_Recurrence",
                table: "MaintenanceItems");

            migrationBuilder.DropColumn(
                name: "IsUnderWarranty",
                table: "MaintenanceLogs");

            migrationBuilder.DropColumn(
                name: "LastServiceIsUnderWarranty",
                table: "MaintenanceItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceItem_Recurrence",
                table: "MaintenanceItems",
                sql: "[IntervalKm] IS NOT NULL OR [IntervalMonths] IS NOT NULL");
        }
    }
}
