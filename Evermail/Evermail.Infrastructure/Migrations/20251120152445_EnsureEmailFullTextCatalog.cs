using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evermail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureEmailFullTextCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') <> 1
                BEGIN
                    THROW 51000, 'Full-text search is not installed on this SQL Server instance. Install the Full-Text feature before running Evermail migrations.', 1;
                END;

                DECLARE @db sysname = DB_NAME();

                IF EXISTS (SELECT 1 FROM sys.databases WHERE name = @db AND is_fulltext_enabled = 0)
                BEGIN
                    IF OBJECT_ID('sp_fulltext_database') IS NOT NULL
                    BEGIN
                        EXEC sp_fulltext_database 'enable';
                    END
                    ELSE
                    BEGIN
                        THROW 51001, 'Full-text search is disabled for this database and cannot be enabled automatically. Provision SQL with Full-Text Search support.', 1;
                    END
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'EmailSearchCatalog')
                BEGIN
                    CREATE FULLTEXT CATALOG EmailSearchCatalog AS DEFAULT;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes fi
                    JOIN sys.objects o ON fi.object_id = o.object_id
                    WHERE o.name = 'EmailMessages'
                )
                BEGIN
                    DROP FULLTEXT INDEX ON EmailMessages;
                END;

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
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes fi
                        JOIN sys.objects o ON fi.object_id = o.object_id
                        WHERE o.name = 'EmailMessages'
                    )
                    BEGIN
                        DROP FULLTEXT INDEX ON EmailMessages;
                    END;

                    IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'EmailSearchCatalog')
                    BEGIN
                        DROP FULLTEXT CATALOG EmailSearchCatalog;
                    END;
                END;
                """,
                suppressTransaction: true);
        }
    }
}
