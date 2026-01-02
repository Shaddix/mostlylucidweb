using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class MarkdownFetcher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarkdownFetches",
                schema: "mostlylucid",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PollFrequencyHours = table.Column<int>(type: "integer", nullable: false),
                    LastFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    CachedContent = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BlogPostId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkdownFetches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarkdownFetches_BlogPosts_BlogPostId",
                        column: x => x.BlogPostId,
                        principalSchema: "mostlylucid",
                        principalTable: "BlogPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFetches_BlogPostId",
                schema: "mostlylucid",
                table: "MarkdownFetches",
                column: "BlogPostId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFetches_IsEnabled",
                schema: "mostlylucid",
                table: "MarkdownFetches",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFetches_LastFetchedAt",
                schema: "mostlylucid",
                table: "MarkdownFetches",
                column: "LastFetchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFetches_Url_BlogPostId",
                schema: "mostlylucid",
                table: "MarkdownFetches",
                columns: new[] { "Url", "BlogPostId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarkdownFetches",
                schema: "mostlylucid");
        }
    }
}
