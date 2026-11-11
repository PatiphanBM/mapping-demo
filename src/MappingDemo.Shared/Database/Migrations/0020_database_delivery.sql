ALTER TABLE mapping_configs
ADD COLUMN draft_destination_definition jsonb;

ALTER TABLE mapping_config_versions
DROP CONSTRAINT mapping_config_versions_destination_kind_check;

ALTER TABLE mapping_config_versions
ADD CONSTRAINT mapping_config_versions_destination_kind_check
CHECK (
    destination_definition IS NULL OR (
        jsonb_typeof(destination_definition) = 'object'
        AND destination_definition ? 'kind'
        AND jsonb_typeof(destination_definition -> 'kind') = 'string'
        AND destination_definition ->> 'kind' IN (
            'legacyTable',
            'apiContract',
            'jsonFile',
            'databaseTable'
        )
    )
);

CREATE TABLE database_delivery_jobs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    file_job_id bigint NOT NULL REFERENCES file_jobs(id),
    row_job_id bigint NOT NULL REFERENCES row_jobs(id),
    source_row_id bigint NOT NULL,
    source_row_number integer NOT NULL CHECK (source_row_number > 0),
    config_version_id bigint NOT NULL REFERENCES mapping_config_versions(id),
    output_key text NOT NULL CHECK (length(btrim(output_key)) > 0),
    operation_kind text NOT NULL CHECK (
        operation_kind IN ('Initial', 'Reprocess', 'Resend')),
    destination_profile_id text NOT NULL,
    provider text NOT NULL CHECK (provider IN ('PostgreSql', 'SqlServer')),
    profile_configuration_hash text NOT NULL,
    target_schema text NOT NULL,
    target_table text NOT NULL,
    target_schema_snapshot jsonb NOT NULL,
    target_schema_hash text NOT NULL,
    duplicate_policy jsonb NOT NULL,
    mapped_columns jsonb NOT NULL CHECK (
        jsonb_typeof(mapped_columns) = 'array'),
    mapped_values_ciphertext bytea NOT NULL,
    mapped_values_nonce bytea NOT NULL CHECK (
        octet_length(mapped_values_nonce) = 12),
    mapped_values_tag bytea NOT NULL CHECK (
        octet_length(mapped_values_tag) = 16),
    mapped_values_hash text NOT NULL,
    encryption_key_reference text NOT NULL,
    status text NOT NULL CHECK (
        status IN (
            'Pending',
            'Delivering',
            'Delivered',
            'AlreadyDelivered',
            'RetryableFailed',
            'PermanentFailed',
            'DeliveryConflict',
            'BlockedSchemaDrift',
            'UnknownOutcome')),
    attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    next_attempt_at timestamptz,
    dispatch_scheduled_at timestamptz,
    lease_owner text,
    lease_expires_at timestamptz,
    correlation_id text,
    last_error_redacted text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    delivered_at timestamptz,
    terminal_at timestamptz,
    payload_expires_at timestamptz,
    payload_purged_at timestamptz,
    audit_expires_at timestamptz,
    UNIQUE (row_job_id, output_key)
);

CREATE INDEX ix_database_delivery_jobs_ready
ON database_delivery_jobs (next_attempt_at, id)
WHERE status IN ('Pending', 'RetryableFailed');

CREATE INDEX ix_database_delivery_jobs_stale_lease
ON database_delivery_jobs (lease_expires_at, id)
WHERE status = 'Delivering';

CREATE INDEX ix_database_delivery_jobs_file_status
ON database_delivery_jobs (file_job_id, status);

CREATE TABLE database_delivery_attempts (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    database_delivery_job_id bigint NOT NULL
        REFERENCES database_delivery_jobs(id) ON DELETE CASCADE,
    attempt_no integer NOT NULL CHECK (attempt_no > 0),
    started_at timestamptz NOT NULL,
    finished_at timestamptz NOT NULL,
    outcome text NOT NULL CHECK (
        outcome IN (
            'Delivered',
            'AlreadyDelivered',
            'RetryableFailed',
            'PermanentFailed',
            'DeliveryConflict',
            'BlockedSchemaDrift',
            'UnknownOutcome')),
    duration_ms integer NOT NULL CHECK (duration_ms >= 0),
    correlation_id text,
    error_code text,
    error_redacted text,
    target_schema_hash text,
    mapped_values_hash text NOT NULL,
    UNIQUE (database_delivery_job_id, attempt_no)
);

CREATE TABLE database_delivery_resolution_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    database_delivery_job_id bigint NOT NULL
        REFERENCES database_delivery_jobs(id) ON DELETE CASCADE,
    action text NOT NULL CHECK (
        action IN ('Retry', 'MarkDelivered', 'MarkFailed')),
    requested_by text NOT NULL CHECK (length(btrim(requested_by)) > 0),
    reason text NOT NULL CHECK (length(btrim(reason)) BETWEEN 1 AND 500),
    correlation_id text NOT NULL CHECK (length(btrim(correlation_id)) > 0),
    requested_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE database_target_schema_checks (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    destination_profile_id text NOT NULL,
    target_schema text NOT NULL,
    target_table text NOT NULL,
    configured_schema_hash text NOT NULL,
    current_schema_hash text,
    compatibility text NOT NULL CHECK (
        compatibility IN (
            'Healthy', 'CompatibleChange', 'BreakingChange', 'Unavailable')),
    diagnostics jsonb NOT NULL,
    checked_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_database_target_schema_checks_latest
ON database_target_schema_checks (
    destination_profile_id,
    target_schema,
    target_table,
    checked_at DESC);

ALTER TABLE delivery_runtime_control
ADD COLUMN database_table_activation_enabled boolean NOT NULL DEFAULT false,
ADD COLUMN database_delivery_paused boolean NOT NULL DEFAULT true;

ALTER TABLE delivery_control_audit
ADD COLUMN database_table_activation_enabled boolean NOT NULL DEFAULT false,
ADD COLUMN database_delivery_paused boolean NOT NULL DEFAULT true;

CREATE UNIQUE INDEX ux_outbox_database_delivery
ON outbox (topic, message_key)
WHERE topic = 'mapping.database-delivery'
  AND sent_at IS NULL;
