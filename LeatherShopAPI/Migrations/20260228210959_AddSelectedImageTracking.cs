using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectedImageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SelectedImageId",
                table: "OrderItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingImageId",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SelectedImageId",
                table: "CartItems",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1966), new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1968) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1973), new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1974) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1976), new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1976) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1977), new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1978) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1998), new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(1999) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(2000), new DateTime(2026, 2, 28, 21, 9, 55, 359, DateTimeKind.Utc).AddTicks(2001) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedImageId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PendingImageId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SelectedImageId",
                table: "CartItems");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6074), new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6075) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6081), new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6081) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6083), new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6083) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6085), new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6085) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6086), new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6087) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6088), new DateTime(2026, 2, 28, 11, 3, 5, 687, DateTimeKind.Utc).AddTicks(6088) });
        }
    }
}
