using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IssuesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeploymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalUrl = table.Column<string>(type: "text", nullable: true),
                    ExternalNumber = table.Column<int>(type: "integer", nullable: true),
                    ExternalState = table.Column<string>(type: "text", nullable: true),
                    ExternalCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuesSet", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssuesSet_DeploymentId",
                table: "IssuesSet",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuesSet_EnvironmentId",
                table: "IssuesSet",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuesSet_ServiceId_Source_ExternalNumber",
                table: "IssuesSet",
                columns: new[] { "ServiceId", "Source", "ExternalNumber" },
                unique: true,
                filter: "\"ExternalNumber\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_IssuesSet_ServiceId_Status",
                table: "IssuesSet",
                columns: new[] { "ServiceId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssuesSet");
        }
    }
}
