using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartDroneDelivery.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEtaPredictionInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputFingerprint",
                table: "ai_eta_predictions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelVersion",
                table: "ai_eta_predictions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputFingerprint",
                table: "ai_eta_predictions");

            migrationBuilder.DropColumn(
                name: "ModelVersion",
                table: "ai_eta_predictions");
        }
    }
}
