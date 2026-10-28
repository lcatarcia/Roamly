using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequirePasswordChangeForProvisionedUsers : Migration
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

            migrationBuilder.Sql(
                """
                EXEC sys.sp_executesql N'
                    UPDATE [AspNetUsers]
                    SET [RequiresPasswordChange] = 1
                    WHERE EXISTS
                    (
                        SELECT 1
                        FROM [AspNetUserClaims]
                        WHERE [AspNetUserClaims].[UserId] = [AspNetUsers].[Id]
                          AND [AspNetUserClaims].[ClaimType] = N''urn:roamly:identity:provisioned-unconfirmed-login''
                    );';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresPasswordChange",
                table: "AspNetUsers");
        }
    }
}
