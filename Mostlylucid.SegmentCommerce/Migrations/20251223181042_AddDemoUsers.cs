using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demo_users",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    persona = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    avatar_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interests = table.Column<Dictionary<string, double>>(type: "jsonb", nullable: false),
                    brand_affinities = table.Column<Dictionary<string, double>>(type: "jsonb", nullable: false),
                    price_min = table.Column<decimal>(type: "numeric", nullable: true),
                    price_max = table.Column<decimal>(type: "numeric", nullable: true),
                    preferred_tags = table.Column<List<string>>(type: "jsonb", nullable: false),
                    segments = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demo_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_demo_users_persistent_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "persistent_profiles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_demo_users_profile_id",
                table: "demo_users",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_demo_users_sort_order",
                table: "demo_users",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demo_users");
        }
    }
}
