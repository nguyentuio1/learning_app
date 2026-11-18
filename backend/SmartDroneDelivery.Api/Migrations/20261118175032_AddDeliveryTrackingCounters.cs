using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartDroneDelivery.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryTrackingCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_tracking_counters",
                columns: table => new
                {
                    Month = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_tracking_counters", x => x.Month);
                });

            // Initialize from numeric suffixes, not lexical ordering (10000 must follow 9999).
            // Migration transaction blocks writes while the existing monthly high-water marks are read.
            migrationBuilder.Sql("""
                LOCK TABLE delivery_orders IN SHARE ROW EXCLUSIVE MODE;
                INSERT INTO delivery_tracking_counters ("Month", "LastValue")
                SELECT substring("TrackingNumber" from '^DD-([0-9]{6})-[0-9]+$'),
                       max(substring("TrackingNumber" from '^DD-[0-9]{6}-([0-9]+)$')::bigint)
                FROM delivery_orders
                WHERE "TrackingNumber" ~ '^DD-[0-9]{6}-[0-9]{1,18}$'
                GROUP BY substring("TrackingNumber" from '^DD-([0-9]{6})-[0-9]+$');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_tracking_counters");
        }
    }
}
