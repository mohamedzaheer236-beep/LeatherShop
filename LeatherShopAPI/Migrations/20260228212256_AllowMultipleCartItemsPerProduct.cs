using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleCartItemsPerProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_CustomerId_ProductId",
                table: "CartItems");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7826), new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7828) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7833), new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7833) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7836), new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7836) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7837), new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7837) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7839), new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7839) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7841), new DateTime(2026, 2, 28, 21, 22, 56, 333, DateTimeKind.Utc).AddTicks(7841) });

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CustomerId_ProductId",
                table: "CartItems",
                columns: new[] { "CustomerId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_CustomerId_ProductId",
                table: "CartItems");

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

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CustomerId_ProductId",
                table: "CartItems",
                columns: new[] { "CustomerId", "ProductId" },
                unique: true);
        }
    }
}
