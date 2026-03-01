using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCarouselBroadcastColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Products",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "CarouselCardsJson",
                table: "BroadcastMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCarousel",
                table: "BroadcastMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1952), new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1953) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1961), new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1961) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1963), new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1964) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1965), new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1965) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1967), new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1967) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1968), new DateTime(2026, 3, 1, 18, 3, 40, 394, DateTimeKind.Utc).AddTicks(1969) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CarouselCardsJson",
                table: "BroadcastMessages");

            migrationBuilder.DropColumn(
                name: "IsCarousel",
                table: "BroadcastMessages");

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
        }
    }
}
