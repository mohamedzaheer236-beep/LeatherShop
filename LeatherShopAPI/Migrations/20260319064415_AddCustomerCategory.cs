using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Customers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "FriendsAndFamily");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Category",
                table: "Customers",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_Category",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Customers");
        }
    }
}
