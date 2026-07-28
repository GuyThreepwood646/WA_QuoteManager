using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QuoteManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeSchemaAndAddInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuoteStatuses",
                columns: table => new
                {
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsTerminal = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteStatuses", x => x.Status);
                });

            migrationBuilder.CreateTable(
                name: "RequestInvitations",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VendorOrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvitedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestInvitations", x => new { x.RequestId, x.VendorOrganizationId });
                    table.ForeignKey(
                        name: "FK_RequestInvitations_Organizations_VendorOrganizationId",
                        column: x => x.VendorOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestInvitations_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "QuoteStatuses",
                columns: new[] { "Status", "DisplayOrder", "IsTerminal" },
                values: new object[,]
                {
                    { "Accepted", 3, true },
                    { "Draft", 0, false },
                    { "Expired", 6, true },
                    { "Rejected", 4, true },
                    { "Submitted", 1, false },
                    { "UnderReview", 2, false },
                    { "Withdrawn", 5, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId",
                table: "Users",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Status_ExpiresAt",
                table: "Quotes",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestInvitations_VendorOrganizationId",
                table: "RequestInvitations",
                column: "VendorOrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Organizations_VendorOrganizationId",
                table: "Quotes",
                column: "VendorOrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_QuoteStatuses_Status",
                table: "Quotes",
                column: "Status",
                principalTable: "QuoteStatuses",
                principalColumn: "Status",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_Organizations_ClientOrganizationId",
                table: "Requests",
                column: "ClientOrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Organizations_VendorOrganizationId",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_QuoteStatuses_Status",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Requests_Organizations_ClientOrganizationId",
                table: "Requests");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "QuoteStatuses");

            migrationBuilder.DropTable(
                name: "RequestInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_Status_ExpiresAt",
                table: "Quotes");
        }
    }
}
