using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SearchUxPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID(N'[PinnedEmailThreads]', N'U') IS NOT NULL DROP TABLE [PinnedEmailThreads];");
            migrationBuilder.Sql("IF OBJECT_ID(N'[SavedSearchFilters]', N'U') IS NOT NULL DROP TABLE [SavedSearchFilters];");
            migrationBuilder.Sql("IF OBJECT_ID(N'[UserDisplaySettings]', N'U') IS NOT NULL DROP TABLE [UserDisplaySettings];");

            migrationBuilder.CreateTable(
                name: "PinnedEmailThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmailMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PinnedEmailThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PinnedEmailThreads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PinnedEmailThreads_EmailMessages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "EmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PinnedEmailThreads_EmailThreads_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "EmailThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PinnedEmailThreads_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedSearchFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearchFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedSearchFilters_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedSearchFilters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDisplaySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateFormat = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResultDensity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AutoScrollToKeyword = table.Column<bool>(type: "bit", nullable: false),
                    MatchNavigatorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    KeyboardShortcutsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDisplaySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDisplaySettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDisplaySettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PinnedEmailThreads_ConversationId",
                table: "PinnedEmailThreads",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedEmailThreads_EmailMessageId",
                table: "PinnedEmailThreads",
                column: "EmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedEmailThreads_TenantId_UserId",
                table: "PinnedEmailThreads",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PinnedEmailThreads_TenantId_UserId_ConversationId",
                table: "PinnedEmailThreads",
                columns: new[] { "TenantId", "UserId", "ConversationId" },
                unique: true,
                filter: "[ConversationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedEmailThreads_TenantId_UserId_EmailMessageId",
                table: "PinnedEmailThreads",
                columns: new[] { "TenantId", "UserId", "EmailMessageId" },
                unique: true,
                filter: "[EmailMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedEmailThreads_UserId",
                table: "PinnedEmailThreads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearchFilters_TenantId_UserId_OrderIndex",
                table: "SavedSearchFilters",
                columns: new[] { "TenantId", "UserId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearchFilters_UserId",
                table: "SavedSearchFilters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDisplaySettings_TenantId_UserId",
                table: "UserDisplaySettings",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDisplaySettings_UserId",
                table: "UserDisplaySettings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PinnedEmailThreads");

            migrationBuilder.DropTable(
                name: "SavedSearchFilters");

            migrationBuilder.DropTable(
                name: "UserDisplaySettings");
        }
    }
}
