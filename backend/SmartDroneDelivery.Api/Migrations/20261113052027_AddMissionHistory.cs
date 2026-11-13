using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartDroneDelivery.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flight_missions_OrderId",
                table: "flight_missions");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "flight_missions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedArrivalTime",
                table: "flight_missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDepartureTime",
                table: "flight_missions",
                type: "timestamp with time zone",
                nullable: true);

            // Preserve useful legacy ordering and active intervals; do not infer old
            // completed/aborted schedules from an order that may have been rescheduled.
            migrationBuilder.Sql("""
                UPDATE flight_missions AS m
                SET "CreatedAt" = COALESCE(m."StartTime", m."EndTime", o."CreatedAt"),
                    "ScheduledDepartureTime" = CASE WHEN m."FlightStatus" IN ('PLANNED', 'IN_AIR') THEN o."ScheduledDepartureTime" END,
                    "EstimatedArrivalTime" = CASE WHEN m."FlightStatus" IN ('PLANNED', 'IN_AIR') THEN o."EstimatedDeliveryTime" END
                FROM delivery_orders AS o WHERE o."Id" = m."OrderId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_flight_missions_OrderId_Active",
                table: "flight_missions",
                column: "OrderId",
                unique: true,
                filter: "\"FlightStatus\" IN ('PLANNED', 'IN_AIR')");

            migrationBuilder.CreateIndex(
                name: "IX_flight_missions_OrderId_CreatedAt",
                table: "flight_missions",
                columns: new[] { "OrderId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flight_missions_OrderId_Active",
                table: "flight_missions");

            migrationBuilder.DropIndex(
                name: "IX_flight_missions_OrderId_CreatedAt",
                table: "flight_missions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "flight_missions");

            migrationBuilder.DropColumn(
                name: "EstimatedArrivalTime",
                table: "flight_missions");

            migrationBuilder.DropColumn(
                name: "ScheduledDepartureTime",
                table: "flight_missions");

            migrationBuilder.CreateIndex(
                name: "IX_flight_missions_OrderId",
                table: "flight_missions",
                column: "OrderId",
                unique: true);
        }
    }
}
