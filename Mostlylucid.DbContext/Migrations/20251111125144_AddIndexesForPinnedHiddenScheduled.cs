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
                name: "IX_BlogPosts_IsHidden_ScheduledPublishDate",
                schema: "mostlylucid",
                table: "BlogPosts",
                columns: new[] { "IsHidden", "ScheduledPublishDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_IsPinned_PublishedDate",
                schema: "mostlylucid",
                table: "BlogPosts",
                columns: new[] { "IsPinned", "PublishedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_IsHidden_ScheduledPublishDate",
                schema: "mostlylucid",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_IsPinned_PublishedDate",
                schema: "mostlylucid",
                table: "BlogPosts");
        }
    }
}
