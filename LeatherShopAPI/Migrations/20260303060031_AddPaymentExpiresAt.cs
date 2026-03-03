using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentExpiresAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2795), new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2797) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2804), new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2804) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2806), new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2806) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2807), new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2807) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2809), new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2809) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2810), new DateTime(2026, 3, 3, 6, 0, 25, 913, DateTimeKind.Utc).AddTicks(2811) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "Orders");

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
    }
}
