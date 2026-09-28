CREATE TABLE file_jobs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    config_id bigint NOT NULL REFERENCES mapping_configs(id),
    config_version_id bigint NOT NULL REFERENCES mapping_config_versions(id),
    original_path text NOT NULL,
    file_name text NOT NULL,
    intake_key text NOT NULL,
    import_status text NOT NULL,
    archive_status text NOT NULL,
    content_hash text,
    snapshot_path text,
    total_rows integer,
    archive_path text,
    last_error text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (config_id, intake_key)
);

CREATE UNIQUE INDEX ux_file_jobs_config_content_hash
ON file_jobs (config_id, content_hash)
WHERE content_hash IS NOT NULL
  AND import_status <> 'Duplicate';

CREATE TABLE outbox (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    topic text NOT NULL,
    message_key text NOT NULL,
    payload jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    sent_at timestamptz
);

CREATE INDEX ix_outbox_unsent
ON outbox (id)
WHERE sent_at IS NULL;
