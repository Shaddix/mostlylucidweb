using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadedImagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DownloadedImages",
                schema: "mostlylucid",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostSlug = table.Column<string>(type: "text", nullable: false),
                    OriginalUrl = table.Column<string>(type: "text", nullable: false),
                    LocalFileName = table.Column<string>(type: "text", nullable: false),
                    DownloadedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastVerifiedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadedImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadedImages_LastVerifiedDate",
                schema: "mostlylucid",
                table: "DownloadedImages",
                column: "LastVerifiedDate");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadedImages_OriginalUrl",
                schema: "mostlylucid",
                table: "DownloadedImages",
                column: "OriginalUrl");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadedImages_PostSlug",
                schema: "mostlylucid",
                table: "DownloadedImages",
                column: "PostSlug");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadedImages_PostSlug_OriginalUrl",
                schema: "mostlylucid",
                table: "DownloadedImages",
                columns: new[] { "PostSlug", "OriginalUrl" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DownloadedImages",
                schema: "mostlylucid");
        }
    }
}
