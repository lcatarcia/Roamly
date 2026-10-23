using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginCounterKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CounterKind",
                table: "LoginAttemptCounters",
                type: "varchar(24)",
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttemptCounters_CounterKind_ExpiresAtUtc",
                table: "LoginAttemptCounters",
                columns: new[] { "CounterKind", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoginAttemptCounters_CounterKind_ExpiresAtUtc",
                table: "LoginAttemptCounters");

            migrationBuilder.DropColumn(
                name: "CounterKind",
                table: "LoginAttemptCounters");
        }
    }
}
