using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingProductId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingProductId",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7611), new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7613) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7618), new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7618) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7620), new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7620) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7622), new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7622) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7623), new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7623) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7643), new DateTime(2026, 2, 21, 12, 37, 13, 677, DateTimeKind.Utc).AddTicks(7643) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingProductId",
                table: "Customers");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3744), new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3746) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3751), new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3752) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3753), new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3753) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3755), new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3755) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3756), new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3756) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3758), new DateTime(2026, 2, 21, 9, 43, 58, 186, DateTimeKind.Utc).AddTicks(3758) });
        }
    }
}
