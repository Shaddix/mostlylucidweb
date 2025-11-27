using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokenLinksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "broken_links",
                schema: "mostlylucid",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    original_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    archive_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_broken = table.Column<bool>(type: "boolean", nullable: false),
                    last_status_code = table.Column<int>(type: "integer", nullable: true),
                    discovered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    archive_checked = table.Column<bool>(type: "boolean", nullable: false),
                    archive_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broken_links", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_broken_links_is_broken_archive_checked",
                schema: "mostlylucid",
                table: "broken_links",
                columns: new[] { "is_broken", "archive_checked" });

            migrationBuilder.CreateIndex(
                name: "IX_broken_links_last_checked_at",
                schema: "mostlylucid",
                table: "broken_links",
                column: "last_checked_at");

            migrationBuilder.CreateIndex(
                name: "IX_broken_links_original_url",
                schema: "mostlylucid",
                table: "broken_links",
                column: "original_url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "broken_links",
                schema: "mostlylucid");
        }
    }
}
