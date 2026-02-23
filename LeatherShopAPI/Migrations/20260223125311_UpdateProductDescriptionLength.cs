using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductDescriptionLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5744), new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5745) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5753), new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5753) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5755), new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5755) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5756), new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5756) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5757), new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5758) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5759), new DateTime(2026, 2, 23, 12, 53, 11, 666, DateTimeKind.Utc).AddTicks(5759) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(672), new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(674) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(680), new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(680) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(682), new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(682) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(684), new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(684) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(685), new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(685) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(687), new DateTime(2026, 2, 23, 12, 40, 12, 984, DateTimeKind.Utc).AddTicks(687) });
        }
    }
}
