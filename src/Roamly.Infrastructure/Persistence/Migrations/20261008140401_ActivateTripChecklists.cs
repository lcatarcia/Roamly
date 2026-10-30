using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivateTripChecklists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Origin = table.Column<byte>(type: "tinyint", nullable: false),
                    SeedTemplateKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SeedCatalogVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checklists", x => new { x.OwnerId, x.Id })
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_Checklist_SeedProvenance", "([Origin] = 1 AND [SeedTemplateKey] IS NOT NULL) OR ([Origin] = 0 AND [SeedTemplateKey] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Checklists_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Checklists_Trips_OwnerId_TripId",
                        columns: x => new { x.OwnerId, x.TripId },
                        principalTable: "Trips",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => new { x.OwnerId, x.Id })
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Checklists_OwnerId_ChecklistId",
                        columns: x => new { x.OwnerId, x.ChecklistId },
                        principalTable: "Checklists",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_OwnerId_ChecklistId",
                table: "ChecklistItems",
                columns: new[] { "OwnerId", "ChecklistId" });

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_OwnerId_TripId",
                table: "Checklists",
                columns: new[] { "OwnerId", "TripId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "Checklists");
        }
    }
}
