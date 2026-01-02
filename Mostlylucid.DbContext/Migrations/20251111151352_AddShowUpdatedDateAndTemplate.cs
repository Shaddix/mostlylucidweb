using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class AddShowUpdatedDateAndTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "show_updated_date",
                schema: "mostlylucid",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "updated_template",
                schema: "mostlylucid",
                table: "BlogPosts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "show_updated_date",
                schema: "mostlylucid",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "updated_template",
                schema: "mostlylucid",
                table: "BlogPosts");
        }
    }
}
