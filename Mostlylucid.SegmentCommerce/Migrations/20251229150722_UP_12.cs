using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Migrations
{
    /// <inheritdoc />
    public partial class UP_12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_signals_session_profiles_session_id",
                table: "signals");

            migrationBuilder.DropTable(
                name: "session_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    persistent_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cart_adds = table.Column<int>(type: "integer", nullable: false),
                    context = table.Column<SessionContext>(type: "jsonb", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    identification_mode = table.Column<int>(type: "integer", nullable: false),
                    interests = table.Column<Dictionary<string, double>>(type: "jsonb", nullable: false),
                    is_elevated = table.Column<bool>(type: "boolean", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    page_views = table.Column<int>(type: "integer", nullable: false),
                    product_views = table.Column<int>(type: "integer", nullable: false),
                    session_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    signal_count = table.Column<int>(type: "integer", nullable: false),
                    signals = table.Column<Dictionary<string, Dictionary<string, int>>>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_weight = table.Column<double>(type: "double precision", nullable: false),
                    viewed_products = table.Column<List<int>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_profiles_persistent_profiles_persistent_profile_id",
                        column: x => x.persistent_profile_id,
                        principalTable: "persistent_profiles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_expires_at",
                table: "session_profiles",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_interests",
                table: "session_profiles",
                column: "interests")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_persistent_profile_id",
                table: "session_profiles",
                column: "persistent_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_session_key",
                table: "session_profiles",
                column: "session_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_profiles_signals",
                table: "session_profiles",
                column: "signals")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.AddForeignKey(
                name: "FK_signals_session_profiles_session_id",
                table: "signals",
                column: "session_id",
                principalTable: "session_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
