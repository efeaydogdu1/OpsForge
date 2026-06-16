using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubRepositoryIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositorySyncRunsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceRepositoryLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RepositoryOwner = table.Column<string>(type: "text", nullable: false),
                    RepositoryName = table.Column<string>(type: "text", nullable: false),
                    RepositoryUrl = table.Column<string>(type: "text", nullable: false),
                    DefaultBranch = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Visibility = table.Column<string>(type: "text", nullable: true),
                    PrimaryLanguage = table.Column<string>(type: "text", nullable: true),
                    LatestCommitSha = table.Column<string>(type: "text", nullable: true),
                    LatestCommitDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestCommitMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySyncRunsSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRepositoryLinksSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryOwner = table.Column<string>(type: "text", nullable: false),
                    RepositoryName = table.Column<string>(type: "text", nullable: false),
                    RepositoryUrl = table.Column<string>(type: "text", nullable: false),
                    DefaultBranch = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Visibility = table.Column<string>(type: "text", nullable: true),
                    PrimaryLanguage = table.Column<string>(type: "text", nullable: true),
                    LatestCommitSha = table.Column<string>(type: "text", nullable: true),
                    LatestCommitDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestCommitMessage = table.Column<string>(type: "text", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRepositoryLinksSet", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySyncRunsSet_ServiceId_StartedAtUtc",
                table: "RepositorySyncRunsSet",
                columns: new[] { "ServiceId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRepositoryLinksSet_RepositoryOwner_RepositoryName",
                table: "ServiceRepositoryLinksSet",
                columns: new[] { "RepositoryOwner", "RepositoryName" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRepositoryLinksSet_ServiceId",
                table: "ServiceRepositoryLinksSet",
                column: "ServiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositorySyncRunsSet");

            migrationBuilder.DropTable(
                name: "ServiceRepositoryLinksSet");
        }
    }
}
