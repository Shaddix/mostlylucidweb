using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class MakeMarkdownFetchBlogPostIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarkdownFetches_Url_BlogPostId",
                schema: "mostlylucid",
                table: "MarkdownFetches");

            migrationBuilder.AlterColumn<int>(
                name: "BlogPostId",
                schema: "mostlylucid",
                table: "MarkdownFetches",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFetches_Url",
                schema: "mostlylucid",
                table: "MarkdownFetches",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarkdownFetches_Url",
                schema: "mostlylucid",
                table: "MarkdownFetches");

            migrationBuilder.AlterColumn<int>(
                name: "BlogPostId",
                schema: "mostlylucid",
                table: "MarkdownFetches",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFetches_Url_BlogPostId",
                schema: "mostlylucid",
                table: "MarkdownFetches",
                columns: new[] { "Url", "BlogPostId" },
                unique: true);
        }
    }
}
