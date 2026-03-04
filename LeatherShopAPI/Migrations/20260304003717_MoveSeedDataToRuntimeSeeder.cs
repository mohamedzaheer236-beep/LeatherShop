using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeatherShopAPI.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// This migration is intentionally empty. It removes HasData seed entries from the model snapshot
    /// without deleting the existing rows from the database. Product seeding is now handled at runtime
    /// by DataSeeder.SeedAsync(), which only inserts if the Products table is empty.
    /// </remarks>
    public partial class MoveSeedDataToRuntimeSeeder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: existing seed data rows remain in the database.
            // Future migrations will no longer contain HasData timestamp noise.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: nothing was changed in Up.
        }
    }
}
