using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZeroAccessDeterministicTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionKeyFingerprint",
                table: "MailboxUploads",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionKeyFingerprint",
                table: "Mailboxes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZeroAccessTokenSalt",
                table: "Mailboxes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantEncryptionBundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WrappedDek = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Nonce = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(88)", maxLength: 88, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantEncryptionBundles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantEncryptionBundles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZeroAccessMailboxTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TokenValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZeroAccessMailboxTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZeroAccessMailboxTokens_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantEncryptionBundles_TenantId_CreatedAt",
                table: "TenantEncryptionBundles",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ZeroAccessMailboxTokens_MailboxId",
                table: "ZeroAccessMailboxTokens",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_ZeroAccessMailboxTokens_TenantId_MailboxId",
                table: "ZeroAccessMailboxTokens",
                columns: new[] { "TenantId", "MailboxId" });

            migrationBuilder.CreateIndex(
                name: "IX_ZeroAccessMailboxTokens_TenantId_TokenType_TokenValue",
                table: "ZeroAccessMailboxTokens",
                columns: new[] { "TenantId", "TokenType", "TokenValue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantEncryptionBundles");

            migrationBuilder.DropTable(
                name: "ZeroAccessMailboxTokens");

            migrationBuilder.DropColumn(
                name: "EncryptionKeyFingerprint",
                table: "MailboxUploads");

            migrationBuilder.DropColumn(
                name: "EncryptionKeyFingerprint",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "ZeroAccessTokenSalt",
                table: "Mailboxes");
        }
    }
}
