using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartDroneDelivery.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulatorDiversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "simulator_diversions",
                columns: table => new
                {
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbortedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AbortLatitude = table.Column<decimal>(type: "numeric(12,9)", precision: 12, scale: 9, nullable: false),
                    AbortLongitude = table.Column<decimal>(type: "numeric(12,9)", precision: 12, scale: 9, nullable: false),
                    CruiseSpeedMps = table.Column<double>(type: "double precision", nullable: false),
                    LandingStationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LandingLatitude = table.Column<decimal>(type: "numeric(12,9)", precision: 12, scale: 9, nullable: true),
                    LandingLongitude = table.Column<decimal>(type: "numeric(12,9)", precision: 12, scale: 9, nullable: true),
                    NavigationStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LandingDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LandedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulator_diversions", x => x.MissionId);
                    table.ForeignKey(
                        name: "FK_simulator_diversions_flight_missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "flight_missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_simulator_diversions_landing_stations_LandingStationId",
                        column: x => x.LandingStationId,
                        principalTable: "landing_stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_simulator_diversions_LandingStationId",
                table: "simulator_diversions",
                column: "LandingStationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "simulator_diversions");
        }
    }
}
