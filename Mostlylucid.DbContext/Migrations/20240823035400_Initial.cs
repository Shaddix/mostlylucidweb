using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Mostlylucid.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mostlylucid");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "mostlylucid",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                schema: "mostlylucid",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "BlogPosts",
                schema: "mostlylucid",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    updated_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    markdown = table.Column<string>(type: "text", nullable: false),
                    html_content = table.Column<string>(type: "text", nullable: false),
                    plain_text_content = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    language_id = table.Column<int>(type: "integer", nullable: false),
                    published_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false, computedColumnSql: "to_tsvector('english', coalesce(title, '') || ' ' || coalesce(plain_text_content, ''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPosts", x => x.id);
                    table.ForeignKey(
                        name: "FK_BlogPosts_languages_language_id",
                        column: x => x.language_id,
                        principalSchema: "mostlylucid",
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blogpostcategory",
                schema: "mostlylucid",
                columns: table => new
                {
                    BlogPostId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blogpostcategory", x => new { x.BlogPostId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_blogpostcategory_BlogPosts_BlogPostId",
                        column: x => x.BlogPostId,
                        principalSchema: "mostlylucid",
                        principalTable: "BlogPosts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_blogpostcategory_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "mostlylucid",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "mostlylucid",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    content = table.Column<string>(type: "text", nullable: false),
                    moderated = table.Column<bool>(type: "boolean", nullable: false),
                    date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    avatar = table.Column<string>(type: "text", nullable: true),
                    slug = table.Column<string>(type: "text", nullable: false),
                    blog_post_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comments_BlogPosts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalSchema: "mostlylucid",
                        principalTable: "BlogPosts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blogpostcategory_CategoryId",
                schema: "mostlylucid",
                table: "blogpostcategory",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_content_hash",
                schema: "mostlylucid",
                table: "BlogPosts",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_language_id",
                schema: "mostlylucid",
                table: "BlogPosts",
                column: "language_id");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_published_date",
                schema: "mostlylucid",
                table: "BlogPosts",
                column: "published_date");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_search_vector",
                schema: "mostlylucid",
                table: "BlogPosts",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_slug_language_id",
                schema: "mostlylucid",
                table: "BlogPosts",
                columns: new[] { "slug", "language_id" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_name",
                schema: "mostlylucid",
                table: "categories",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_comments_blog_post_id",
                schema: "mostlylucid",
                table: "comments",
                column: "blog_post_id");

            migrationBuilder.CreateIndex(
                name: "IX_comments_date",
                schema: "mostlylucid",
                table: "comments",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "IX_comments_moderated",
                schema: "mostlylucid",
                table: "comments",
                column: "moderated");

            migrationBuilder.CreateIndex(
                name: "IX_comments_slug",
                schema: "mostlylucid",
                table: "comments",
                column: "slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blogpostcategory",
                schema: "mostlylucid");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "mostlylucid");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "mostlylucid");

            migrationBuilder.DropTable(
                name: "BlogPosts",
                schema: "mostlylucid");

            migrationBuilder.DropTable(
                name: "languages",
                schema: "mostlylucid");
        }
    }
}
