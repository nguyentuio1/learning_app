using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartDroneDelivery.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Action_Timestamp",
                table: "audit_logs",
                columns: new[] { "Action", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Timestamp",
                table: "audit_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId_Timestamp",
                table: "audit_logs",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_Action_Timestamp",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_Timestamp",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_UserId_Timestamp",
                table: "audit_logs");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");
        }
    }
}
