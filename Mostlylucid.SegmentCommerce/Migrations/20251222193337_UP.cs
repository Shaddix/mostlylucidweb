using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Migrations
{
    /// <inheritdoc />
    public partial class UP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_aggregate_id",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_next_retry_at",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_processed_at_created_at",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_job_queue_created_at",
                table: "job_queue");

            migrationBuilder.DropIndex(
                name: "IX_job_queue_job_type",
                table: "job_queue");

            migrationBuilder.DropIndex(
                name: "IX_job_queue_queue_status_scheduled_at_priority",
                table: "job_queue");

            migrationBuilder.DropIndex(
                name: "IX_job_queue_started_at",
                table: "job_queue");

            migrationBuilder.DropIndex(
                name: "IX_job_queue_status",
                table: "job_queue");

            migrationBuilder.DropIndex(
                name: "IX_interest_embeddings_session_id",
                table: "interest_embeddings");

            migrationBuilder.AddColumn<string>(
                name: "brand",
                table: "products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "compare_at_price",
                table: "products",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "handle",
                table: "products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_description",
                table: "products",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_title",
                table: "products",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "subcategory",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "availability_status",
                table: "product_variations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "barcode",
                table: "product_variations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "compare_at_price",
                table: "product_variations",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gtin",
                table: "product_variations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "taxonomy_nodes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    handle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    shopify_taxonomy_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    path = table.Column<string>(type: "ltree", nullable: false),
                    attributes = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxonomy_nodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_taxonomy_nodes_taxonomy_nodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "taxonomy_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    store_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_store_products_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_store_products_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    store_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_store_users_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_taxonomy",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    taxonomy_node_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_taxonomy", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_taxonomy_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_taxonomy_taxonomy_nodes_taxonomy_node_id",
                        column: x => x.taxonomy_node_id,
                        principalTable: "taxonomy_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_handle",
                table: "products",
                column: "handle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_taxonomy_product_id_taxonomy_node_id",
                table: "product_taxonomy",
                columns: new[] { "product_id", "taxonomy_node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_taxonomy_taxonomy_node_id",
                table: "product_taxonomy",
                column: "taxonomy_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_products_product_id",
                table: "store_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_products_store_id_product_id",
                table: "store_products",
                columns: new[] { "store_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_users_store_id_user_id",
                table: "store_users",
                columns: new[] { "store_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stores_slug",
                table: "stores",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_taxonomy_nodes_handle",
                table: "taxonomy_nodes",
                column: "handle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_taxonomy_nodes_ParentId",
                table: "taxonomy_nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_taxonomy_nodes_path",
                table: "taxonomy_nodes",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_taxonomy_nodes_shopify_taxonomy_id",
                table: "taxonomy_nodes",
                column: "shopify_taxonomy_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_taxonomy");

            migrationBuilder.DropTable(
                name: "store_products");

            migrationBuilder.DropTable(
                name: "store_users");

            migrationBuilder.DropTable(
                name: "taxonomy_nodes");

            migrationBuilder.DropTable(
                name: "stores");

            migrationBuilder.DropIndex(
                name: "IX_products_handle",
                table: "products");

            migrationBuilder.DropColumn(
                name: "brand",
                table: "products");

            migrationBuilder.DropColumn(
                name: "compare_at_price",
                table: "products");

            migrationBuilder.DropColumn(
                name: "handle",
                table: "products");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "products");

            migrationBuilder.DropColumn(
                name: "seo_description",
                table: "products");

            migrationBuilder.DropColumn(
                name: "seo_title",
                table: "products");

            migrationBuilder.DropColumn(
                name: "status",
                table: "products");

            migrationBuilder.DropColumn(
                name: "subcategory",
                table: "products");

            migrationBuilder.DropColumn(
                name: "availability_status",
                table: "product_variations");

            migrationBuilder.DropColumn(
                name: "barcode",
                table: "product_variations");

            migrationBuilder.DropColumn(
                name: "compare_at_price",
                table: "product_variations");

            migrationBuilder.DropColumn(
                name: "gtin",
                table: "product_variations");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_aggregate_id",
                table: "outbox_messages",
                column: "aggregate_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_next_retry_at",
                table: "outbox_messages",
                column: "next_retry_at",
                filter: "processed_at IS NULL AND next_retry_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_at_created_at",
                table: "outbox_messages",
                columns: new[] { "processed_at", "created_at" },
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_job_queue_created_at",
                table: "job_queue",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_job_queue_job_type",
                table: "job_queue",
                column: "job_type");

            migrationBuilder.CreateIndex(
                name: "IX_job_queue_queue_status_scheduled_at_priority",
                table: "job_queue",
                columns: new[] { "queue", "status", "scheduled_at", "priority" },
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "IX_job_queue_started_at",
                table: "job_queue",
                column: "started_at",
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_job_queue_status",
                table: "job_queue",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_interest_embeddings_session_id",
                table: "interest_embeddings",
                column: "session_id");
        }
    }
}
