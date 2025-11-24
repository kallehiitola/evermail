using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGdprSelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDataExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDataExports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDeletionJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDeletionJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDataExports_TenantId_Status",
                table: "UserDataExports",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDataExports_TenantId_UserId",
                table: "UserDataExports",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDeletionJobs_TenantId_UserId",
                table: "UserDeletionJobs",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDataExports");

            migrationBuilder.DropTable(
                name: "UserDeletionJobs");
        }
    }
}
