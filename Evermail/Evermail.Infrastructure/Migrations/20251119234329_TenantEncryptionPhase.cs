using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantEncryptionPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailboxEncryptionStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxUploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WrappedDek = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DekVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TenantKeyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastKeyReleaseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastKeyReleaseComponent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastKeyReleaseLedgerEntryId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AttestationPolicyId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KeyVaultKeyVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxEncryptionStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailboxEncryptionStates_MailboxUploads_MailboxUploadId",
                        column: x => x.MailboxUploadId,
                        principalTable: "MailboxUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MailboxEncryptionStates_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenantEncryptionSettings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyVaultUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    KeyVaultKeyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KeyVaultKeyVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KeyVaultTenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ManagedIdentityObjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EncryptionPhase = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastVerificationMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSecureKeyReleaseConfigured = table.Column<bool>(type: "bit", nullable: false),
                    SecureKeyReleaseConfiguredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SecureKeyReleaseConfiguredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SecureKeyReleasePolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecureKeyReleasePolicyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AttestationProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantEncryptionSettings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantEncryptionSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxEncryptionStates_MailboxId",
                table: "MailboxEncryptionStates",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxEncryptionStates_MailboxUploadId",
                table: "MailboxEncryptionStates",
                column: "MailboxUploadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailboxEncryptionStates_TenantId_CreatedAt",
                table: "MailboxEncryptionStates",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailboxEncryptionStates");

            migrationBuilder.DropTable(
                name: "TenantEncryptionSettings");
        }
    }
}
