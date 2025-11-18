using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MailboxLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Mailboxes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPendingDeletion",
                table: "Mailboxes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LatestUploadId",
                table: "Mailboxes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurgeAfter",
                table: "Mailboxes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoftDeletedAt",
                table: "Mailboxes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SoftDeletedByUserId",
                table: "Mailboxes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadRemovedAt",
                table: "Mailboxes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadRemovedByUserId",
                table: "Mailboxes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ContentHash",
                table: "EmailMessages",
                type: "varbinary(900)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MailboxUploadId",
                table: "EmailMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MailboxDeletionQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxUploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeleteUpload = table.Column<bool>(type: "bit", nullable: false),
                    DeleteEmails = table.Column<bool>(type: "bit", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecuteAfter = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxDeletionQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailboxDeletionQueue_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MailboxUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalEmails = table.Column<int>(type: "int", nullable: false),
                    ProcessedEmails = table.Column<int>(type: "int", nullable: false),
                    FailedEmails = table.Column<int>(type: "int", nullable: false),
                    ProcessedBytes = table.Column<long>(type: "bigint", nullable: false),
                    KeepEmails = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurgeAfter = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailboxUploads_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mailboxes_IsPendingDeletion_PurgeAfter",
                table: "Mailboxes",
                columns: new[] { "IsPendingDeletion", "PurgeAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_Mailboxes_LatestUploadId",
                table: "Mailboxes",
                column: "LatestUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_MailboxId_ContentHash",
                table: "EmailMessages",
                columns: new[] { "MailboxId", "ContentHash" },
                unique: true,
                filter: "[ContentHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_MailboxUploadId",
                table: "EmailMessages",
                column: "MailboxUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_MessageId",
                table: "EmailMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxDeletionQueue_MailboxId",
                table: "MailboxDeletionQueue",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxDeletionQueue_Status_ExecuteAfter",
                table: "MailboxDeletionQueue",
                columns: new[] { "Status", "ExecuteAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxUploads_MailboxId",
                table: "MailboxUploads",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxUploads_Status",
                table: "MailboxUploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxUploads_TenantId",
                table: "MailboxUploads",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailMessages_MailboxUploads_MailboxUploadId",
                table: "EmailMessages",
                column: "MailboxUploadId",
                principalTable: "MailboxUploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Mailboxes_MailboxUploads_LatestUploadId",
                table: "Mailboxes",
                column: "LatestUploadId",
                principalTable: "MailboxUploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailMessages_MailboxUploads_MailboxUploadId",
                table: "EmailMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Mailboxes_MailboxUploads_LatestUploadId",
                table: "Mailboxes");

            migrationBuilder.DropTable(
                name: "MailboxDeletionQueue");

            migrationBuilder.DropTable(
                name: "MailboxUploads");

            migrationBuilder.DropIndex(
                name: "IX_Mailboxes_IsPendingDeletion_PurgeAfter",
                table: "Mailboxes");

            migrationBuilder.DropIndex(
                name: "IX_Mailboxes_LatestUploadId",
                table: "Mailboxes");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_MailboxId_ContentHash",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_MailboxUploadId",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_MessageId",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "IsPendingDeletion",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "LatestUploadId",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "PurgeAfter",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "SoftDeletedAt",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "SoftDeletedByUserId",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "UploadRemovedAt",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "UploadRemovedByUserId",
                table: "Mailboxes");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "MailboxUploadId",
                table: "EmailMessages");
        }
    }
}
