using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.DbContext.Migrations
{
    /// <inheritdoc />
    public partial class FixCommentClosureCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fully idempotent: Only operate if both Comments and comment_closures tables exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Only proceed if both tables exist
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'mostlylucid'
                        AND table_name = 'Comments'
                    ) AND EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'mostlylucid'
                        AND table_name = 'comment_closures'
                    ) THEN
                        -- Drop constraint if it exists
                        IF EXISTS (
                            SELECT 1 FROM information_schema.table_constraints
                            WHERE constraint_schema = 'mostlylucid'
                            AND table_name = 'comment_closures'
                            AND constraint_name = 'FK_comment_closures_Comments_ancestor_id'
                        ) THEN
                            ALTER TABLE mostlylucid.comment_closures
                            DROP CONSTRAINT ""FK_comment_closures_Comments_ancestor_id"";
                        END IF;

                        -- Add constraint if it doesn't exist
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.table_constraints
                            WHERE constraint_schema = 'mostlylucid'
                            AND table_name = 'comment_closures'
                            AND constraint_name = 'FK_comment_closures_Comments_ancestor_id'
                        ) THEN
                            ALTER TABLE mostlylucid.comment_closures
                            ADD CONSTRAINT ""FK_comment_closures_Comments_ancestor_id""
                            FOREIGN KEY (ancestor_id) REFERENCES mostlylucid.""Comments""(""Id"") ON DELETE CASCADE;
                        END IF;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comment_closures_Comments_ancestor_id",
                schema: "mostlylucid",
                table: "comment_closures");

            migrationBuilder.AddForeignKey(
                name: "FK_comment_closures_Comments_ancestor_id",
                schema: "mostlylucid",
                table: "comment_closures",
                column: "ancestor_id",
                principalSchema: "mostlylucid",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
