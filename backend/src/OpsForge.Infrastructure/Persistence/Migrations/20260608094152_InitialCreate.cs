using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogsSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    CommitHash = table.Column<string>(type: "text", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "text", nullable: true),
                    DeploymentDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeployedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentsSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InfrastructureAssetsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AssetType = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    ResourceIdentifier = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfrastructureAssetsSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServicesSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OwnerTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Criticality = table.Column<int>(type: "integer", nullable: false),
                    RepositoryUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicesSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamsSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsersSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsersSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceEnvironmentsSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceEnvironmentsSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceEnvironmentsSet_ServicesSet_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "ServicesSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceInfrastructureLinksSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InfrastructureAssetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceInfrastructureLinksSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceInfrastructureLinksSet_InfrastructureAssetsSet_Infra~",
                        column: x => x.InfrastructureAssetId,
                        principalTable: "InfrastructureAssetsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceInfrastructureLinksSet_ServicesSet_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "ServicesSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembersSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembersSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembersSet_TeamsSet_TeamId",
                        column: x => x.TeamId,
                        principalTable: "TeamsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokensSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokensSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokensSet_UsersSet_UserId",
                        column: x => x.UserId,
                        principalTable: "UsersSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokensSet_TokenHash",
                table: "RefreshTokensSet",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokensSet_UserId",
                table: "RefreshTokensSet",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceEnvironmentsSet_ServiceId_Name",
                table: "ServiceEnvironmentsSet",
                columns: new[] { "ServiceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInfrastructureLinksSet_InfrastructureAssetId",
                table: "ServiceInfrastructureLinksSet",
                column: "InfrastructureAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceInfrastructureLinksSet_ServiceId_InfrastructureAsset~",
                table: "ServiceInfrastructureLinksSet",
                columns: new[] { "ServiceId", "InfrastructureAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicesSet_Name",
                table: "ServicesSet",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembersSet_TeamId",
                table: "TeamMembersSet",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamsSet_Name",
                table: "TeamsSet",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsersSet_Email",
                table: "UsersSet",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogsSet");

            migrationBuilder.DropTable(
                name: "DeploymentsSet");

            migrationBuilder.DropTable(
                name: "RefreshTokensSet");

            migrationBuilder.DropTable(
                name: "ServiceEnvironmentsSet");

            migrationBuilder.DropTable(
                name: "ServiceInfrastructureLinksSet");

            migrationBuilder.DropTable(
                name: "TeamMembersSet");

            migrationBuilder.DropTable(
                name: "UsersSet");

            migrationBuilder.DropTable(
                name: "InfrastructureAssetsSet");

            migrationBuilder.DropTable(
                name: "ServicesSet");

            migrationBuilder.DropTable(
                name: "TeamsSet");
        }
    }
}
