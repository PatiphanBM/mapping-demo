CREATE TABLE row_jobs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    file_job_id bigint NOT NULL REFERENCES file_jobs(id),
    source_row_id bigint NOT NULL,
    row_number integer NOT NULL,
    config_version_id bigint NOT NULL REFERENCES mapping_config_versions(id),
    kind text NOT NULL CHECK (kind IN ('Initial', 'Reprocess')),
    status text NOT NULL,
    attempts integer NOT NULL DEFAULT 0,
    last_error text,
    created_at timestamptz NOT NULL DEFAULT now(),
    finished_at timestamptz
);

CREATE UNIQUE INDEX ux_row_jobs_pending_source_row
ON row_jobs (source_row_id)
WHERE status = 'Pending';

CREATE TABLE row_errors (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    row_job_id bigint NOT NULL REFERENCES row_jobs(id),
    file_job_id bigint NOT NULL REFERENCES file_jobs(id),
    row_number integer NOT NULL,
    field text NOT NULL,
    reason text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
