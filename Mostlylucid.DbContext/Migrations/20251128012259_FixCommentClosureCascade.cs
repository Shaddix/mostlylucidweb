using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class FixCommentClosureCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comment_closures_Comments_ancestor_id",
                schema: "mostlylucid",
                table: "comment_closures");

            migrationBuilder.AddForeignKey(
                name: "FK_comment_closures_Comments_ancestor_id",
                schema: "mostlylucid",
                table: "comment_closures",
                column: "ancestor_id",
                principalSchema: "mostlylucid",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comment_closures_Comments_ancestor_id",
                schema: "mostlylucid",
                table: "comment_closures");

            migrationBuilder.AddForeignKey(
                name: "FK_comment_closures_Comments_ancestor_id",
                schema: "mostlylucid",
                table: "comment_closures",
                column: "ancestor_id",
                principalSchema: "mostlylucid",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
