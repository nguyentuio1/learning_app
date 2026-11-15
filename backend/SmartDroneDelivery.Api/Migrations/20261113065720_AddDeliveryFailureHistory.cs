using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartDroneDelivery.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryFailureHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_failure_episodes",
                columns: table => new
                {
                    FailureLogId = table.Column<long>(type: "bigint", nullable: false),
                    FailureType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_failure_episodes", x => x.FailureLogId);
                    table.ForeignKey(
                        name: "FK_delivery_failure_episodes_delivery_status_logs_FailureLogId",
                        column: x => x.FailureLogId,
                        principalTable: "delivery_status_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "failure_handling_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FailureLogId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PerformedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_failure_handling_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_failure_handling_events_delivery_status_logs_FailureLogId",
                        column: x => x.FailureLogId,
                        principalTable: "delivery_status_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_failure_handling_events_users_PerformedBy",
                        column: x => x.PerformedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_failure_handling_events_FailureLogId_CreatedAt_Id",
                table: "failure_handling_events",
                columns: new[] { "FailureLogId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_failure_handling_events_PerformedBy",
                table: "failure_handling_events",
                column: "PerformedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_failure_episodes");

            migrationBuilder.DropTable(
                name: "failure_handling_events");
        }
    }
}
