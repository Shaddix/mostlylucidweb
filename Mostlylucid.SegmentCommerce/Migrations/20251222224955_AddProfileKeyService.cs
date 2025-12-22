using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileKeyService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_profile_keys_anonymous_profiles_profile_id",
                table: "profile_keys");

            migrationBuilder.DropForeignKey(
                name: "FK_session_profiles_anonymous_profiles_anonymous_profile_id",
                table: "session_profiles");

            migrationBuilder.DropTable(
                name: "interest_scores");

            migrationBuilder.DropTable(
                name: "anonymous_profiles");

            migrationBuilder.DropIndex(
                name: "IX_session_profiles_profile_key",
                table: "session_profiles");

            migrationBuilder.DropIndex(
                name: "IX_profile_keys_derivation_method",
                table: "profile_keys");

            migrationBuilder.DropIndex(
                name: "IX_profile_keys_key_hash",
                table: "profile_keys");

            migrationBuilder.DropColumn(
                name: "profile_key",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "promotion_threshold",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "derivation_method",
                table: "profile_keys");

            migrationBuilder.DropColumn(
                name: "source_hint",
                table: "profile_keys");

            migrationBuilder.RenameColumn(
                name: "last_seen_at",
                table: "session_profiles",
                newName: "last_activity_at");

            migrationBuilder.RenameColumn(
                name: "is_promoted",
                table: "session_profiles",
                newName: "is_elevated");

            migrationBuilder.RenameColumn(
                name: "anonymous_profile_id",
                table: "session_profiles",
                newName: "persistent_profile_id");

            migrationBuilder.RenameIndex(
                name: "IX_session_profiles_anonymous_profile_id",
                table: "session_profiles",
                newName: "IX_session_profiles_persistent_profile_id");

            migrationBuilder.RenameColumn(
                name: "key_hash",
                table: "profile_keys",
                newName: "key_value");

            migrationBuilder.AddColumn<int>(
                name: "cart_adds",
                table: "session_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<SessionContext>(
                name: "context",
                table: "session_profiles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "identification_mode",
                table: "session_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Dictionary<string, double>>(
                name: "interests",
                table: "session_profiles",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "page_views",
                table: "session_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "product_views",
                table: "session_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Dictionary<string, Dictionary<string, int>>>(
                name: "signals",
                table: "session_profiles",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<List<int>>(
                name: "viewed_products",
                table: "session_profiles",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "key_type",
                table: "profile_keys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_used_at",
                table: "profile_keys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "persistent_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    identification_mode = table.Column<int>(type: "integer", nullable: false),
                    interests = table.Column<Dictionary<string, double>>(type: "jsonb", nullable: false),
                    affinities = table.Column<Dictionary<string, double>>(type: "jsonb", nullable: false),
                    brand_affinities = table.Column<Dictionary<string, double>>(type: "jsonb", nullable: false),
                    price_preferences = table.Column<PricePreferences>(type: "jsonb", nullable: true),
                    traits = table.Column<Dictionary<string, bool>>(type: "jsonb", nullable: false),
                    segments = table.Column<long>(type: "bigint", nullable: false),
                    llm_segments = table.Column<Dictionary<string, double>>(type: "jsonb", nullable: true),
                    segments_computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    embedding_computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_sessions = table.Column<int>(type: "integer", nullable: false),
                    total_signals = table.Column<int>(type: "integer", nullable: false),
                    total_purchases = table.Column<int>(type: "integer", nullable: false),
                    total_cart_adds = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persistent_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_interests",
                table: "session_profiles",
                column: "interests")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_signals",
                table: "session_profiles",
                column: "signals")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_profile_keys_key_value_key_type",
                table: "profile_keys",
                columns: new[] { "key_value", "key_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_persistent_profiles_affinities",
                table: "persistent_profiles",
                column: "affinities")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_persistent_profiles_embedding",
                table: "persistent_profiles",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_persistent_profiles_identification_mode",
                table: "persistent_profiles",
                column: "identification_mode");

            migrationBuilder.CreateIndex(
                name: "IX_persistent_profiles_interests",
                table: "persistent_profiles",
                column: "interests")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_persistent_profiles_last_seen_at",
                table: "persistent_profiles",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "IX_persistent_profiles_llm_segments",
                table: "persistent_profiles",
                column: "llm_segments")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_persistent_profiles_profile_key",
                table: "persistent_profiles",
                column: "profile_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_persistent_profiles_segments",
                table: "persistent_profiles",
                column: "segments");

            migrationBuilder.AddForeignKey(
                name: "FK_profile_keys_persistent_profiles_profile_id",
                table: "profile_keys",
                column: "profile_id",
                principalTable: "persistent_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_session_profiles_persistent_profiles_persistent_profile_id",
                table: "session_profiles",
                column: "persistent_profile_id",
                principalTable: "persistent_profiles",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_profile_keys_persistent_profiles_profile_id",
                table: "profile_keys");

            migrationBuilder.DropForeignKey(
                name: "FK_session_profiles_persistent_profiles_persistent_profile_id",
                table: "session_profiles");

            migrationBuilder.DropTable(
                name: "persistent_profiles");

            migrationBuilder.DropIndex(
                name: "IX_session_profiles_interests",
                table: "session_profiles");

            migrationBuilder.DropIndex(
                name: "IX_session_profiles_signals",
                table: "session_profiles");

            migrationBuilder.DropIndex(
                name: "IX_profile_keys_key_value_key_type",
                table: "profile_keys");

            migrationBuilder.DropColumn(
                name: "cart_adds",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "context",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "identification_mode",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "interests",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "page_views",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "product_views",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "signals",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "viewed_products",
                table: "session_profiles");

            migrationBuilder.DropColumn(
                name: "key_type",
                table: "profile_keys");

            migrationBuilder.DropColumn(
                name: "last_used_at",
                table: "profile_keys");

            migrationBuilder.RenameColumn(
                name: "persistent_profile_id",
                table: "session_profiles",
                newName: "anonymous_profile_id");

            migrationBuilder.RenameColumn(
                name: "last_activity_at",
                table: "session_profiles",
                newName: "last_seen_at");

            migrationBuilder.RenameColumn(
                name: "is_elevated",
                table: "session_profiles",
                newName: "is_promoted");

            migrationBuilder.RenameIndex(
                name: "IX_session_profiles_persistent_profile_id",
                table: "session_profiles",
                newName: "IX_session_profiles_anonymous_profile_id");

            migrationBuilder.RenameColumn(
                name: "key_value",
                table: "profile_keys",
                newName: "key_hash");

            migrationBuilder.AddColumn<string>(
                name: "profile_key",
                table: "session_profiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "promotion_threshold",
                table: "session_profiles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.AddColumn<string>(
                name: "derivation_method",
                table: "profile_keys",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "source_hint",
                table: "profile_keys",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "anonymous_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    age = table.Column<int>(type: "integer", nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    birth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dislikes = table.Column<List<string>>(type: "jsonb", nullable: true),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    likes = table.Column<List<string>>(type: "jsonb", nullable: true),
                    profile_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    profile_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    signal_count = table.Column<int>(type: "integer", nullable: false),
                    total_weight = table.Column<double>(type: "double precision", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anonymous_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interest_scores",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    decay_rate = table.Column<double>(type: "double precision", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interest_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_interest_scores_anonymous_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "anonymous_profiles",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_interest_scores_session_profiles_session_id",
                        column: x => x.session_id,
                        principalTable: "session_profiles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_profile_key",
                table: "session_profiles",
                column: "profile_key");

            migrationBuilder.CreateIndex(
                name: "IX_profile_keys_derivation_method",
                table: "profile_keys",
                column: "derivation_method");

            migrationBuilder.CreateIndex(
                name: "IX_profile_keys_key_hash",
                table: "profile_keys",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anonymous_profiles_last_seen_at",
                table: "anonymous_profiles",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "IX_anonymous_profiles_profile_key",
                table: "anonymous_profiles",
                column: "profile_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_interest_scores_profile_id_category",
                table: "interest_scores",
                columns: new[] { "profile_id", "category" },
                unique: true,
                filter: "profile_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_interest_scores_session_id_category",
                table: "interest_scores",
                columns: new[] { "session_id", "category" },
                unique: true,
                filter: "session_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_profile_keys_anonymous_profiles_profile_id",
                table: "profile_keys",
                column: "profile_id",
                principalTable: "anonymous_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_session_profiles_anonymous_profiles_anonymous_profile_id",
                table: "session_profiles",
                column: "anonymous_profile_id",
                principalTable: "anonymous_profiles",
                principalColumn: "id");
        }
    }
}
