using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPreferredVendor",
                table: "Organizations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryAddress",
                table: "Organizations",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryContactEmail",
                table: "Organizations",
                type: "TEXT",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryContactName",
                table: "Organizations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryContactPhone",
                table: "Organizations",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationLocations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationLocations_OrganizationId",
                table: "OrganizationLocations",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationLocations");

            migrationBuilder.DropColumn(
                name: "IsPreferredVendor",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PrimaryAddress",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PrimaryContactEmail",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PrimaryContactName",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PrimaryContactPhone",
                table: "Organizations");
        }
    }
}
