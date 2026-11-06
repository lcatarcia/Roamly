using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roamly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMapsEconomicGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapsEconomicStates",
                columns: table => new
                {
                    ResourceId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SuspensionReason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SuspendedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    AlertPending = table.Column<bool>(type: "bit", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ReportedAmountEur = table.Column<double>(type: "float", nullable: true),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CoveredThroughUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapsEconomicStates", x => x.ResourceId);
                    table.CheckConstraint("CK_MapsEconomicState_Evidence", "([ReportedAmountEur] IS NULL AND [ObservedAtUtc] IS NULL AND [CoveredThroughUtc] IS NULL) OR ([ReportedAmountEur] IS NOT NULL AND [ReportedAmountEur] >= 0 AND [ObservedAtUtc] IS NOT NULL AND [CoveredThroughUtc] IS NOT NULL AND [CoveredThroughUtc] >= [PeriodStartUtc] AND [CoveredThroughUtc] <= [ObservedAtUtc] AND [ObservedAtUtc] < [PeriodEndUtc])");
                    table.CheckConstraint("CK_MapsEconomicState_Lease", "([LeaseId] IS NULL AND [LeaseExpiresAtUtc] IS NULL) OR ([LeaseId] IS NOT NULL AND [LeaseExpiresAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_MapsEconomicState_Period", "[PeriodEndUtc] > [PeriodStartUtc] AND [Revision] >= 1 AND LEN([ResourceId]) > 0");
                    table.CheckConstraint("CK_MapsEconomicState_Suspension", "([SuspensionReason] IS NULL AND [SuspendedAtUtc] IS NULL AND [AlertPending] = 0) OR ([SuspensionReason] IS NOT NULL AND [SuspensionReason] = 'ThresholdReached' AND [SuspendedAtUtc] IS NOT NULL AND [AlertPending] = 1 AND [ReportedAmountEur] IS NOT NULL AND [ReportedAmountEur] >= 1)");
                    table.CheckConstraint("CK_MapsEconomicState_Threshold", "[ReportedAmountEur] IS NULL OR [ReportedAmountEur] < 1 OR [SuspensionReason] IS NOT NULL");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapsEconomicStates");
        }
    }
}
