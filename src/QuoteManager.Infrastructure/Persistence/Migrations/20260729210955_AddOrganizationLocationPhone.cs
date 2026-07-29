using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationLocationPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "OrganizationLocations",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                table: "OrganizationLocations");
        }
    }
}
