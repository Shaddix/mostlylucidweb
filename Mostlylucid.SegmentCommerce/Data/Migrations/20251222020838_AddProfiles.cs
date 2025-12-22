using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anonymous_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    total_weight = table.Column<double>(type: "double precision", nullable: false),
                    signal_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anonymous_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "profile_keys",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    derivation_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_hint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_profile_keys_anonymous_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "anonymous_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    profile_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    anonymous_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_weight = table.Column<double>(type: "double precision", nullable: false),
                    signal_count = table.Column<int>(type: "integer", nullable: false),
                    promotion_threshold = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.5),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_promoted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_profiles_anonymous_profiles_anonymous_profile_id",
                        column: x => x.anonymous_profile_id,
                        principalTable: "anonymous_profiles",
                        principalColumn: "id");
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
                    score = table.Column<double>(type: "double precision", nullable: false),
                    decay_rate = table.Column<double>(type: "double precision", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "signals",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    product_id = table.Column<int>(type: "integer", nullable: true),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    context = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    page_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    referrer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signals", x => x.id);
                    table.ForeignKey(
                        name: "FK_signals_session_profiles_session_id",
                        column: x => x.session_id,
                        principalTable: "session_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_profile_keys_profile_id",
                table: "profile_keys",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_anonymous_profile_id",
                table: "session_profiles",
                column: "anonymous_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_expires_at",
                table: "session_profiles",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_profile_key",
                table: "session_profiles",
                column: "profile_key");

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_session_key",
                table: "session_profiles",
                column: "session_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signals_category",
                table: "signals",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_signals_created_at",
                table: "signals",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_signals_session_id",
                table: "signals",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_signals_signal_type",
                table: "signals",
                column: "signal_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interest_scores");

            migrationBuilder.DropTable(
                name: "profile_keys");

            migrationBuilder.DropTable(
                name: "signals");

            migrationBuilder.DropTable(
                name: "session_profiles");

            migrationBuilder.DropTable(
                name: "anonymous_profiles");
        }
    }
}
