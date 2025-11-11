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
                name: "ShowUpdatedDate",
                schema: "mostlylucid",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedTemplate",
                schema: "mostlylucid",
                table: "BlogPosts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowUpdatedDate",
                schema: "mostlylucid",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "UpdatedTemplate",
                schema: "mostlylucid",
                table: "BlogPosts");
        }
    }
}
