using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePageUrlToBrokenLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_page_url",
                schema: "mostlylucid",
                table: "broken_links",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_page_url",
                schema: "mostlylucid",
                table: "broken_links");
        }
    }
}
