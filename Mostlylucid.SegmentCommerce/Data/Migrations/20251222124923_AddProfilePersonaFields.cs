using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilePersonaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "age",
                table: "anonymous_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bio",
                table: "anonymous_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "birth_date",
                table: "anonymous_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "dislikes",
                table: "anonymous_profiles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "anonymous_profiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "likes",
                table: "anonymous_profiles",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "age",
                table: "anonymous_profiles");

            migrationBuilder.DropColumn(
                name: "bio",
                table: "anonymous_profiles");

            migrationBuilder.DropColumn(
                name: "birth_date",
                table: "anonymous_profiles");

            migrationBuilder.DropColumn(
                name: "dislikes",
                table: "anonymous_profiles");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "anonymous_profiles");

            migrationBuilder.DropColumn(
                name: "likes",
                table: "anonymous_profiles");
        }
    }
}
