using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class ContentHashUniquePerLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_ContentHash",
                schema: "mostlylucid",
                table: "BlogPosts");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_ContentHash_LanguageId",
                schema: "mostlylucid",
                table: "BlogPosts",
                columns: new[] { "ContentHash", "LanguageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_ContentHash_LanguageId",
                schema: "mostlylucid",
                table: "BlogPosts");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_ContentHash",
                schema: "mostlylucid",
                table: "BlogPosts",
                column: "ContentHash",
                unique: true);
        }
    }
}
