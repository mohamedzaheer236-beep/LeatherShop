using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Products",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
