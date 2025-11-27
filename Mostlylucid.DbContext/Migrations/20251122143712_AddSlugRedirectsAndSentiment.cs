using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugRedirectsAndSentiment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SentimentMetadata",
                schema: "mostlylucid",
                table: "BlogPosts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "slug_redirects",
                schema: "mostlylucid",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_slug = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    to_slug = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    shown_count = table.Column<int>(type: "integer", nullable: false),
                    confidence_score = table.Column<double>(type: "double precision", nullable: false),
                    auto_redirect = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_clicked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slug_redirects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "slug_suggestion_clicks",
                schema: "mostlylucid",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    requested_slug = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    clicked_slug = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    suggestion_position = table.Column<int>(type: "integer", nullable: false),
                    original_similarity_score = table.Column<double>(type: "double precision", nullable: false),
                    user_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    clicked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slug_suggestion_clicks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_slug_redirects_from_slug_language_auto_redirect",
                schema: "mostlylucid",
                table: "slug_redirects",
                columns: new[] { "from_slug", "language", "auto_redirect" });

            migrationBuilder.CreateIndex(
                name: "IX_slug_redirects_from_slug_to_slug_language",
                schema: "mostlylucid",
                table: "slug_redirects",
                columns: new[] { "from_slug", "to_slug", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slug_suggestion_clicks_clicked_at",
                schema: "mostlylucid",
                table: "slug_suggestion_clicks",
                column: "clicked_at");

            migrationBuilder.CreateIndex(
                name: "IX_slug_suggestion_clicks_requested_slug_clicked_slug_language",
                schema: "mostlylucid",
                table: "slug_suggestion_clicks",
                columns: new[] { "requested_slug", "clicked_slug", "language" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "slug_redirects",
                schema: "mostlylucid");

            migrationBuilder.DropTable(
                name: "slug_suggestion_clicks",
                schema: "mostlylucid");

            migrationBuilder.DropColumn(
                name: "SentimentMetadata",
                schema: "mostlylucid",
                table: "BlogPosts");
        }
    }
}
