using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedHiddenScheduledToBlogPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                schema: "mostlylucid",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                schema: "mostlylucid",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledPublishDate",
                schema: "mostlylucid",
                table: "BlogPosts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHidden",
                schema: "mostlylucid",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                schema: "mostlylucid",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "ScheduledPublishDate",
                schema: "mostlylucid",
                table: "BlogPosts");
        }
    }
}
