using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivateExpenseManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CamperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    IncurredOnUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OdometerKm = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => new { x.OwnerId, x.Id })
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_Expense_Currency", "[Currency] = UPPER([Currency]) AND LEN([Currency]) = 3");
                    table.ForeignKey(
                        name: "FK_Expenses_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Expenses_Campers_OwnerId_CamperId",
                        columns: x => new { x.OwnerId, x.CamperId },
                        principalTable: "Campers",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Expenses_Trips_OwnerId_TripId",
                        columns: x => new { x.OwnerId, x.TripId },
                        principalTable: "Trips",
                        principalColumns: new[] { "OwnerId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_OwnerId_CamperId_IncurredOnUtc",
                table: "Expenses",
                columns: new[] { "OwnerId", "CamperId", "IncurredOnUtc" })
                .Annotation("SqlServer:Include", new[] { "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_OwnerId_TripId",
                table: "Expenses",
                columns: new[] { "OwnerId", "TripId" },
                filter: "[TripId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");
        }
    }
}
