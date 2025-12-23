using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "attributes",
                table: "taxonomy_nodes",
                newName: "Attributes");

            migrationBuilder.RenameColumn(
                name: "context",
                table: "signals",
                newName: "Context");

            migrationBuilder.RenameColumn(
                name: "metadata",
                table: "interaction_events",
                newName: "Metadata");

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    customer_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    shipping_cost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    payment_method = table.Column<int>(type: "integer", nullable: false),
                    payment_status = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    fulfillment_status = table.Column<int>(type: "integer", nullable: false),
                    shipping_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shipping_region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shipping_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    variation_id = table.Column<int>(type: "integer", nullable: true),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    product_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    original_price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_product_variations_variation_id",
                        column: x => x.variation_id,
                        principalTable: "product_variations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_variation_id",
                table: "order_items",
                column: "variation_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_created_at",
                table: "orders",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_number",
                table: "orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_profile_id",
                table: "orders",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_session_key",
                table: "orders",
                column: "session_key");

            migrationBuilder.CreateIndex(
                name: "IX_orders_status",
                table: "orders",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.RenameColumn(
                name: "Attributes",
                table: "taxonomy_nodes",
                newName: "attributes");

            migrationBuilder.RenameColumn(
                name: "Context",
                table: "signals",
                newName: "context");

            migrationBuilder.RenameColumn(
                name: "Metadata",
                table: "interaction_events",
                newName: "metadata");
        }
    }
}
