using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_sellers_seller_id",
                table: "products");

            migrationBuilder.DropTable(
                name: "sellers");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "store_users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "store_users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<Guid>(
                name: "seller_id",
                table: "products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    segment_type = table.Column<int>(type: "integer", nullable: false),
                    rules = table.Column<List<SegmentRuleData>>(type: "jsonb", nullable: false),
                    rule_combination = table.Column<int>(type: "integer", nullable: false),
                    membership_threshold = table.Column<double>(type: "double precision", nullable: false),
                    tags = table.Column<List<string>>(type: "jsonb", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    llm_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    generation_prompt = table.Column<string>(type: "text", nullable: true),
                    member_count = table.Column<int>(type: "integer", nullable: false),
                    member_count_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_segments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seller_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    rating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    review_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_seller_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_store_users_user_id",
                table: "store_users",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_segments_is_active",
                table: "segments",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_segments_segment_type",
                table: "segments",
                column: "segment_type");

            migrationBuilder.CreateIndex(
                name: "IX_segments_slug",
                table: "segments",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_segments_sort_order",
                table: "segments",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "IX_seller_profiles_business_name",
                table: "seller_profiles",
                column: "business_name");

            migrationBuilder.CreateIndex(
                name: "IX_seller_profiles_is_active",
                table: "seller_profiles",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_seller_profiles_is_verified",
                table: "seller_profiles",
                column: "is_verified");

            migrationBuilder.CreateIndex(
                name: "IX_users_created_at",
                table: "users",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_is_active",
                table: "users",
                column: "is_active");

            migrationBuilder.AddForeignKey(
                name: "FK_products_users_seller_id",
                table: "products",
                column: "seller_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_store_users_users_user_id",
                table: "store_users",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_users_seller_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_store_users_users_user_id",
                table: "store_users");

            migrationBuilder.DropTable(
                name: "segments");

            migrationBuilder.DropTable(
                name: "seller_profiles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_store_users_user_id",
                table: "store_users");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "store_users");

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                table: "store_users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "seller_id",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "sellers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    review_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sellers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sellers_email",
                table: "sellers",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sellers_name",
                table: "sellers",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_sellers_seller_id",
                table: "products",
                column: "seller_id",
                principalTable: "sellers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
