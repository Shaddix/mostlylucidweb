using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesForPinnedHiddenScheduled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_is_hidden_scheduled_publish_date",
                schema: "mostlylucid",
                table: "BlogPosts",
                columns: new[] { "is_hidden", "scheduled_publish_date" });

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_is_pinned_published_date",
                schema: "mostlylucid",
                table: "BlogPosts",
                columns: new[] { "is_pinned", "published_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_is_hidden_scheduled_publish_date",
                schema: "mostlylucid",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_is_pinned_published_date",
                schema: "mostlylucid",
                table: "BlogPosts");
        }
    }
}
