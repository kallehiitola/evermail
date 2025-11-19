using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailThreadingAndRecipientIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categories",
                table: "EmailMessages",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "EmailMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConversationKey",
                table: "EmailMessages",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Importance",
                table: "EmailMessages",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ListId",
                table: "EmailMessages",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "EmailMessages",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientsSearch",
                table: "EmailMessages",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToAddress",
                table: "EmailMessages",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnPath",
                table: "EmailMessages",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderAddress",
                table: "EmailMessages",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "EmailMessages",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThreadDepth",
                table: "EmailMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ThreadTopic",
                table: "EmailMessages",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.Sql(
                """
                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    BEGIN TRY
                        IF EXISTS (
                            SELECT 1
                            FROM sys.fulltext_indexes fi
                            JOIN sys.objects o ON fi.object_id = o.object_id
                            WHERE o.name = 'EmailMessages'
                        )
                        BEGIN
                            DROP FULLTEXT INDEX ON EmailMessages;
                        END

                        CREATE FULLTEXT INDEX ON EmailMessages(
                            Subject LANGUAGE 1033,
                            TextBody LANGUAGE 1033,
                            HtmlBody LANGUAGE 1033,
                            RecipientsSearch LANGUAGE 1033,
                            FromName LANGUAGE 1033,
                            FromAddress LANGUAGE 1033
                        )
                        KEY INDEX PK_EmailMessages
                        ON EmailSearchCatalog
                        WITH CHANGE_TRACKING AUTO;
                    END TRY
                    BEGIN CATCH
                        PRINT 'Full-text not available; skipping EmailMessages full-text index creation. Error: ' + ERROR_MESSAGE();
                    END CATCH
                END
                ELSE
                BEGIN
                    PRINT 'Full-text not installed; skipping EmailMessages full-text index creation.';
                END
                """);

            migrationBuilder.CreateTable(
                name: "EmailRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRecipients_EmailMessages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "EmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RootMessageId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ParticipantsSummary = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    FirstMessageDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailThreads_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_ConversationId",
                table: "EmailMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_TenantId_ConversationId",
                table: "EmailMessages",
                columns: new[] { "TenantId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_EmailMessageId",
                table: "EmailRecipients",
                column: "EmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_TenantId_Address",
                table: "EmailRecipients",
                columns: new[] { "TenantId", "Address" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_TenantId_RecipientType_Address",
                table: "EmailRecipients",
                columns: new[] { "TenantId", "RecipientType", "Address" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailThreads_TenantId_ConversationKey",
                table: "EmailThreads",
                columns: new[] { "TenantId", "ConversationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailThreads_TenantId_LastMessageDate",
                table: "EmailThreads",
                columns: new[] { "TenantId", "LastMessageDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmailMessages_EmailThreads_ConversationId",
                table: "EmailMessages",
                column: "ConversationId",
                principalTable: "EmailThreads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailMessages_EmailThreads_ConversationId",
                table: "EmailMessages");

            migrationBuilder.DropTable(
                name: "EmailRecipients");

            migrationBuilder.DropTable(
                name: "EmailThreads");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_ConversationId",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_TenantId_ConversationId",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "Categories",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ConversationKey",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "Importance",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ListId",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "RecipientsSearch",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToAddress",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ReturnPath",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "SenderAddress",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ThreadDepth",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ThreadTopic",
                table: "EmailMessages");

            migrationBuilder.Sql(
                """
                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    BEGIN TRY
                        IF EXISTS (
                            SELECT 1
                            FROM sys.fulltext_indexes fi
                            JOIN sys.objects o ON fi.object_id = o.object_id
                            WHERE o.name = 'EmailMessages'
                        )
                        BEGIN
                            DROP FULLTEXT INDEX ON EmailMessages;
                        END

                        CREATE FULLTEXT INDEX ON EmailMessages(
                            Subject LANGUAGE 1033,
                            TextBody LANGUAGE 1033,
                            FromName LANGUAGE 1033,
                            FromAddress LANGUAGE 1033
                        )
                        KEY INDEX PK_EmailMessages
                        ON EmailSearchCatalog
                        WITH CHANGE_TRACKING AUTO;
                    END TRY
                    BEGIN CATCH
                        PRINT 'Full-text not available; skipping EmailMessages full-text index recreation. Error: ' + ERROR_MESSAGE();
                    END CATCH
                END
                ELSE
                BEGIN
                    PRINT 'Full-text not installed; skipping EmailMessages full-text index recreation.';
                END
                """);
        }
    }
}
