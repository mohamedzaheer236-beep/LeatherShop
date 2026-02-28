using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class PersistBroadcastJobData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "BroadcastMessages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "BroadcastMessages",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "ParametersJson",
                table: "BroadcastMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessedPhonesJson",
                table: "BroadcastMessages",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "RecipientsJson",
                table: "BroadcastMessages",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BroadcastMessages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1184), new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1185) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1191), new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1192) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1194), new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1194) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1195), new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1195) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1197), new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1197) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1199), new DateTime(2026, 2, 28, 22, 29, 12, 329, DateTimeKind.Utc).AddTicks(1199) });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastMessages_Status",
                table: "BroadcastMessages",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BroadcastMessages_Status",
                table: "BroadcastMessages");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "BroadcastMessages");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "BroadcastMessages");

            migrationBuilder.DropColumn(
                name: "ParametersJson",
                table: "BroadcastMessages");

            migrationBuilder.DropColumn(
                name: "ProcessedPhonesJson",
                table: "BroadcastMessages");

            migrationBuilder.DropColumn(
                name: "RecipientsJson",
                table: "BroadcastMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BroadcastMessages");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4889), new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4890) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4898), new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4898) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4900), new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4901) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4902), new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4902) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4904), new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4904) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4906), new DateTime(2026, 2, 28, 22, 12, 3, 370, DateTimeKind.Utc).AddTicks(4906) });
        }
    }
}
