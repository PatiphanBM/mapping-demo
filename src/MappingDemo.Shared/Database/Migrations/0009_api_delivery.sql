CREATE TABLE delivery_jobs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    file_job_id bigint NOT NULL REFERENCES file_jobs(id),
    row_job_id bigint NOT NULL REFERENCES row_jobs(id),
    source_row_id bigint NOT NULL,
    source_row_number integer NOT NULL CHECK (source_row_number > 0),
    config_version_id bigint NOT NULL REFERENCES mapping_config_versions(id),
    output_key text NOT NULL,
    resend_sequence integer NOT NULL DEFAULT 0 CHECK (resend_sequence >= 0),
    operation_kind text NOT NULL CHECK (
        operation_kind IN ('Initial', 'Reprocess', 'Resend')),
    destination_profile_id text NOT NULL,
    contract_id text NOT NULL,
    contract_version text NOT NULL,
    contract_reference text NOT NULL,
    schema_hash text NOT NULL,
    payload_ciphertext bytea NOT NULL,
    payload_nonce bytea NOT NULL,
    payload_tag bytea NOT NULL,
    payload_hash text NOT NULL,
    encryption_key_reference text NOT NULL,
    idempotency_key text NOT NULL,
    status text NOT NULL CHECK (
        status IN (
            'Pending',
            'Delivering',
            'Delivered',
            'RetryableFailed',
            'PermanentFailed')),
    attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    next_attempt_at timestamptz,
    dispatch_scheduled_at timestamptz,
    lease_owner text,
    lease_expires_at timestamptz,
    http_status integer,
    correlation_id text,
    last_error_redacted text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    delivered_at timestamptz,
    terminal_at timestamptz,
    payload_expires_at timestamptz NOT NULL,
    audit_expires_at timestamptz NOT NULL,
    resend_of_delivery_job_id bigint REFERENCES delivery_jobs(id),
    UNIQUE (
        config_version_id,
        file_job_id,
        source_row_id,
        output_key,
        resend_sequence),
    UNIQUE (idempotency_key)
);

CREATE INDEX ix_delivery_jobs_ready
ON delivery_jobs (next_attempt_at, id)
WHERE status IN ('Pending', 'RetryableFailed');

CREATE INDEX ix_delivery_jobs_stale_lease
ON delivery_jobs (lease_expires_at, id)
WHERE status = 'Delivering';

CREATE INDEX ix_delivery_jobs_file_status
ON delivery_jobs (file_job_id, status);

CREATE TABLE delivery_attempts (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    delivery_job_id bigint NOT NULL REFERENCES delivery_jobs(id),
    attempt_no integer NOT NULL CHECK (attempt_no > 0),
    started_at timestamptz NOT NULL,
    finished_at timestamptz NOT NULL,
    outcome text NOT NULL CHECK (
        outcome IN ('Delivered', 'RetryableFailed', 'PermanentFailed')),
    http_status integer,
    duration_ms integer NOT NULL CHECK (duration_ms >= 0),
    correlation_id text,
    error_redacted text,
    payload_hash text NOT NULL,
    idempotency_key text NOT NULL,
    UNIQUE (delivery_job_id, attempt_no)
);

CREATE INDEX ix_delivery_attempts_job
ON delivery_attempts (delivery_job_id, attempt_no);

CREATE TABLE delivery_resend_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    original_delivery_job_id bigint NOT NULL REFERENCES delivery_jobs(id),
    resend_delivery_job_id bigint NOT NULL REFERENCES delivery_jobs(id),
    request_id text NOT NULL,
    requested_by text NOT NULL,
    reason text NOT NULL,
    requested_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (resend_delivery_job_id),
    UNIQUE (original_delivery_job_id, request_id)
);
