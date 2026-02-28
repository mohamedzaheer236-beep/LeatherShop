using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppOutboxTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    To = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Context = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppOutboxMessages", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOutboxMessages_CreatedAt",
                table: "WhatsAppOutboxMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOutboxMessages_Status_NextRetryAt",
                table: "WhatsAppOutboxMessages",
                columns: new[] { "Status", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppOutboxMessages");

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
        }
    }
}
