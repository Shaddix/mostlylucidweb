using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create covering index for common search queries
            // Allows index-only scans without touching the table heap
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_blog_posts_search_covering
                ON mostlylucid.""BlogPosts"" (""LanguageId"", ""IsHidden"", ""ScheduledPublishDate"")
                INCLUDE (""Id"", ""Slug"", ""Title"", ""PublishedDate"")
                WHERE ""IsHidden"" = false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS mostlylucid.idx_blog_posts_search_covering;
            ");
        }
    }
}
