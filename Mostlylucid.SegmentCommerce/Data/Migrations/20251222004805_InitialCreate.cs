using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    css_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    original_price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    is_trending = table.Column<bool>(type: "boolean", nullable: false),
                    is_featured = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "visitor_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    interests = table.Column<Dictionary<string, InterestWeightData>>(type: "jsonb", nullable: false),
                    is_unmasked = table.Column<bool>(type: "boolean", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_visits = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitor_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interaction_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    metadata = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interaction_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_interaction_events_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_interaction_events_visitor_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "visitor_profiles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_interaction_events_category_created_at",
                table: "interaction_events",
                columns: new[] { "category", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_interaction_events_created_at",
                table: "interaction_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_interaction_events_event_type",
                table: "interaction_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_interaction_events_product_id",
                table: "interaction_events",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_interaction_events_profile_id",
                table: "interaction_events",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_interaction_events_session_id",
                table: "interaction_events",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_products_is_featured",
                table: "products",
                column: "is_featured");

            migrationBuilder.CreateIndex(
                name: "IX_products_is_trending",
                table: "products",
                column: "is_trending");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_profiles_last_seen_at",
                table: "visitor_profiles",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_profiles_profile_token",
                table: "visitor_profiles",
                column: "profile_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "interaction_events");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "visitor_profiles");
        }
    }
}
