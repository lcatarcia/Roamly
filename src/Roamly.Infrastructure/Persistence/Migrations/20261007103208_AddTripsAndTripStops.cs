using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripsAndTripStops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedPlaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExternalProvider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ExternalPlaceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExternalFetchedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(8,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedPlaces", x => new { x.OwnerId, x.Id })
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_SavedPlace_Position", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
                    table.ForeignKey(
                        name: "FK_SavedPlaces_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CamperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PlannedStartOnUtc = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedEndOnUtc = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualStartOnUtc = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEndOnUtc = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => new { x.OwnerId, x.Id })
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_Trips_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trips_Campers_OwnerId_CamperId",
                        columns: x => new { x.OwnerId, x.CamperId },
                        principalTable: "Campers",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ArrivalOnUtc = table.Column<DateOnly>(type: "date", nullable: true),
                    DepartureOnUtc = table.Column<DateOnly>(type: "date", nullable: true),
                    SavedPlaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(8,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripStops", x => new { x.OwnerId, x.Id })
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_TripStop_Position", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
                    table.ForeignKey(
                        name: "FK_TripStops_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripStops_SavedPlaces_OwnerId_SavedPlaceId",
                        columns: x => new { x.OwnerId, x.SavedPlaceId },
                        principalTable: "SavedPlaces",
                        principalColumns: new[] { "OwnerId", "Id" });
                    table.ForeignKey(
                        name: "FK_TripStops_Trips_OwnerId_TripId",
                        columns: x => new { x.OwnerId, x.TripId },
                        principalTable: "Trips",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_OwnerId_CamperId",
                table: "Trips",
                columns: new[] { "OwnerId", "CamperId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripStops_OwnerId_SavedPlaceId",
                table: "TripStops",
                columns: new[] { "OwnerId", "SavedPlaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripStops_OwnerId_TripId_SequenceNo",
                table: "TripStops",
                columns: new[] { "OwnerId", "TripId", "SequenceNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripStops");

            migrationBuilder.DropTable(
                name: "SavedPlaces");

            migrationBuilder.DropTable(
                name: "Trips");
        }
    }
}
