-- Enable required PostgreSQL extensions

-- pgvector for vector similarity search
CREATE EXTENSION IF NOT EXISTS vector;

-- For generating UUIDs
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- For full-text search (optional but useful)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- For advisory locks (useful for distributed processing)
-- Already built-in, no extension needed

-- Create a function for advisory locking on job processing
CREATE OR REPLACE FUNCTION try_advisory_lock_job(job_id BIGINT)
RETURNS BOOLEAN AS $$
BEGIN
    RETURN pg_try_advisory_lock(job_id);
END;
$$ LANGUAGE plpgsql;

-- Create a function for releasing advisory locks
CREATE OR REPLACE FUNCTION release_advisory_lock_job(job_id BIGINT)
RETURNS BOOLEAN AS $$
BEGIN
    RETURN pg_advisory_unlock(job_id);
END;
$$ LANGUAGE plpgsql;

-- Create a function for cleaning up old completed jobs
CREATE OR REPLACE FUNCTION cleanup_old_jobs(retention_days INTEGER DEFAULT 7)
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM job_queue
    WHERE status IN (2, 3, 4)  -- Completed, Failed, Cancelled
      AND completed_at < NOW() - (retention_days || ' days')::INTERVAL;
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Create a function for cleaning up old outbox messages
CREATE OR REPLACE FUNCTION cleanup_old_outbox(retention_days INTEGER DEFAULT 30)
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM outbox_messages
    WHERE processed_at IS NOT NULL
      AND processed_at < NOW() - (retention_days || ' days')::INTERVAL;
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Notify function for real-time job processing (optional LISTEN/NOTIFY)
CREATE OR REPLACE FUNCTION notify_new_job()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM pg_notify('new_job', json_build_object(
        'id', NEW.id,
        'queue', NEW.queue,
        'job_type', NEW.job_type
    )::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger will be created after the job_queue table exists
-- CREATE TRIGGER job_queue_notify
--     AFTER INSERT ON job_queue
--     FOR EACH ROW
--     EXECUTE FUNCTION notify_new_job();

COMMENT ON EXTENSION vector IS 'pgvector - vector similarity search for PostgreSQL';
