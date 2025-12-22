using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mostlylucid.SegmentCommerce.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobQueueNotifyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION job_queue_notify() RETURNS trigger AS $$
                BEGIN
                    PERFORM pg_notify('job_queue_notify', NEW.queue);
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS job_queue_notify_insert ON job_queue;
                CREATE TRIGGER job_queue_notify_insert
                AFTER INSERT ON job_queue
                FOR EACH ROW
                WHEN (NEW.status = 0 AND NEW.scheduled_at <= NOW())
                EXECUTE FUNCTION job_queue_notify();

                DROP TRIGGER IF EXISTS job_queue_notify_update ON job_queue;
                CREATE TRIGGER job_queue_notify_update
                AFTER UPDATE OF status, scheduled_at ON job_queue
                FOR EACH ROW
                WHEN (NEW.status = 0 AND NEW.scheduled_at <= NOW())
                EXECUTE FUNCTION job_queue_notify();
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS job_queue_notify_insert ON job_queue;
                DROP TRIGGER IF EXISTS job_queue_notify_update ON job_queue;
                DROP FUNCTION IF EXISTS job_queue_notify();
                """
            );
        }

    }
}
