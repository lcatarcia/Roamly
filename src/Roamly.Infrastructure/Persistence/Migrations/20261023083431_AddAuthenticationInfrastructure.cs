using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresPasswordChange",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginAttemptCounters",
                columns: table => new
                {
                    BucketKey = table.Column<string>(type: "char(64)", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CounterKind = table.Column<string>(type: "varchar(24)", nullable: false, defaultValue: "legacy"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttemptCounters", x => new { x.BucketKey, x.WindowStartUtc });
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttemptCounters_CounterKind_ExpiresAtUtc",
                table: "LoginAttemptCounters",
                columns: new[] { "CounterKind", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttemptCounters_ExpiresAtUtc",
                table: "LoginAttemptCounters",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "LoginAttemptCounters");

            migrationBuilder.DropColumn(
                name: "RequiresPasswordChange",
                table: "AspNetUsers");
        }
    }
}
