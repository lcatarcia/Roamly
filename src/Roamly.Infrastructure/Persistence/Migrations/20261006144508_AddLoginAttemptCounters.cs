using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginAttemptCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoginAttemptCounters",
                columns: table => new
                {
                    BucketKey = table.Column<string>(type: "char(64)", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttemptCounters", x => new { x.BucketKey, x.WindowStartUtc });
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttemptCounters_ExpiresAtUtc",
                table: "LoginAttemptCounters",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginAttemptCounters");
        }
    }
}
