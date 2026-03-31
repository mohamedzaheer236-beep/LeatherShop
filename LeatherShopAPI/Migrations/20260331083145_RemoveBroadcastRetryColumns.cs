using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBroadcastRetryColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BroadcastRecipients_NextRetryAt",
                table: "BroadcastRecipients");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "BroadcastRecipients");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "BroadcastRecipients");

            migrationBuilder.DropColumn(
                name: "RetryHistoryJson",
                table: "BroadcastRecipients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "BroadcastRecipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "BroadcastRecipients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RetryHistoryJson",
                table: "BroadcastRecipients",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_NextRetryAt",
                table: "BroadcastRecipients",
                column: "NextRetryAt",
                filter: "\"NextRetryAt\" IS NOT NULL");
        }
    }
}
