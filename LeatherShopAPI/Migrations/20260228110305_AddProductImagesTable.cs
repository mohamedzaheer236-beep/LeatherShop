using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Products",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Products",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9931), new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9933) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9939), new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9939) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9941), new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9941) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9942), new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9943) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9944), new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9944) });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9946), new DateTime(2026, 2, 25, 21, 11, 11, 816, DateTimeKind.Utc).AddTicks(9946) });
        }
    }
}
