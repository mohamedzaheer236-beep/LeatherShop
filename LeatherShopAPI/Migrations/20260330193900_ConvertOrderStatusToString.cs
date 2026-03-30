using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class ConvertOrderStatusToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Convert existing integer values to their enum string names
            migrationBuilder.Sql("""
                ALTER TABLE "Orders" ALTER COLUMN "Status" TYPE character varying(20)
                USING CASE "Status"
                    WHEN 0 THEN 'Pending'
                    WHEN 1 THEN 'Confirmed'
                    WHEN 2 THEN 'Shipped'
                    WHEN 3 THEN 'Delivered'
                    WHEN 4 THEN 'Cancelled'
                    ELSE 'Pending'
                END;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "AdminNotifications",
                type: "numeric(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert string values back to integers
            migrationBuilder.Sql("""
                ALTER TABLE "Orders" ALTER COLUMN "Status" TYPE integer
                USING CASE "Status"
                    WHEN 'Pending' THEN 0
                    WHEN 'Confirmed' THEN 1
                    WHEN 'Shipped' THEN 2
                    WHEN 'Delivered' THEN 3
                    WHEN 'Cancelled' THEN 4
                    ELSE 0
                END;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "AdminNotifications",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)");
        }
    }
}
