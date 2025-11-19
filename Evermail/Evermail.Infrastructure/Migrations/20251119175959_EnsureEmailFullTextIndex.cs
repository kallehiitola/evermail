using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureEmailFullTextIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                    END TRY
                    BEGIN CATCH
                        PRINT 'Full-text not available; skipping EmailMessages full-text index drop. Error: ' + ERROR_MESSAGE();
                    END CATCH
                END
                """);
        }
    }
}
