using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryHistoryColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalSentAt",
                table: "BroadcastRecipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetryHistoryJson",
                table: "BroadcastRecipients",
                type: "text",
                nullable: true);

            // Backfill: set OriginalSentAt from SentAt for existing records
            migrationBuilder.Sql(
                "UPDATE \"BroadcastRecipients\" SET \"OriginalSentAt\" = \"SentAt\" WHERE \"SentAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalSentAt",
                table: "BroadcastRecipients");

            migrationBuilder.DropColumn(
                name: "RetryHistoryJson",
                table: "BroadcastRecipients");
        }
    }
}
