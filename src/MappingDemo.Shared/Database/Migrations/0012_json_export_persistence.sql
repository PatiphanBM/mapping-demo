CREATE TABLE json_export_jobs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    file_job_id bigint NOT NULL REFERENCES file_jobs(id),
    config_version_id bigint NOT NULL REFERENCES mapping_config_versions(id),
    destination_profile_id text NOT NULL CHECK (
        length(btrim(destination_profile_id)) > 0),
    artifact_revision integer NOT NULL CHECK (artifact_revision > 0),
    status text NOT NULL CHECK (
        status IN (
            'Pending',
            'Finalizing',
            'Published',
            'Blocked',
            'RetryableFailed')),
    relative_temp_path text NOT NULL CHECK (
        length(btrim(relative_temp_path)) > 0),
    relative_final_path text NOT NULL CHECK (
        length(btrim(relative_final_path)) > 0),
    object_count integer CHECK (object_count >= 0),
    byte_count bigint CHECK (byte_count >= 0),
    sha256 text,
    lease_owner text,
    lease_expires_at timestamptz,
    attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    last_error_redacted text,
    previous_export_job_id bigint REFERENCES json_export_jobs(id)
        ON DELETE SET NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    terminal_at timestamptz,
    published_at timestamptz,
    payload_expires_at timestamptz,
    payload_purged_at timestamptz,
    artifact_expires_at timestamptz,
    artifact_deleted_at timestamptz,
    audit_expires_at timestamptz,
    legal_hold boolean NOT NULL DEFAULT false,
    legal_hold_reason text,
    legal_hold_owner text,
    legal_hold_until timestamptz,
    CHECK (
        (
            NOT legal_hold
            AND legal_hold_reason IS NULL
            AND legal_hold_owner IS NULL
            AND legal_hold_until IS NULL
        ) OR (
            legal_hold
            AND legal_hold_reason IS NOT NULL
            AND length(btrim(legal_hold_reason)) > 0
            AND legal_hold_owner IS NOT NULL
            AND length(btrim(legal_hold_owner)) > 0
            AND legal_hold_until IS NOT NULL
        )
    ),
    UNIQUE (file_job_id, config_version_id, artifact_revision),
    UNIQUE (id, file_job_id, config_version_id, artifact_revision)
);

CREATE INDEX ix_json_export_jobs_ready
ON json_export_jobs (created_at, id)
WHERE status IN ('Pending', 'RetryableFailed');

CREATE INDEX ix_json_export_jobs_stale_lease
ON json_export_jobs (lease_expires_at, id)
WHERE status = 'Finalizing';

CREATE INDEX ix_json_export_jobs_file_history
ON json_export_jobs (file_job_id, artifact_revision DESC);

CREATE INDEX ix_json_export_jobs_payload_cleanup
ON json_export_jobs (payload_expires_at, id)
WHERE payload_expires_at IS NOT NULL
  AND payload_purged_at IS NULL
  AND NOT legal_hold;

CREATE INDEX ix_json_export_jobs_artifact_cleanup
ON json_export_jobs (artifact_expires_at, id)
WHERE artifact_expires_at IS NOT NULL
  AND artifact_deleted_at IS NULL
  AND NOT legal_hold;

CREATE INDEX ix_json_export_jobs_audit_cleanup
ON json_export_jobs (audit_expires_at, id)
WHERE audit_expires_at IS NOT NULL
  AND NOT legal_hold;

CREATE TABLE json_export_items (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    json_export_job_id bigint NOT NULL,
    file_job_id bigint NOT NULL,
    config_version_id bigint NOT NULL,
    artifact_revision integer NOT NULL CHECK (artifact_revision > 0),
    row_job_id bigint NOT NULL REFERENCES row_jobs(id),
    source_row_id bigint NOT NULL,
    row_number integer NOT NULL CHECK (row_number > 0),
    output_key text NOT NULL CHECK (length(btrim(output_key)) > 0),
    output_ordinal integer NOT NULL CHECK (output_ordinal >= 0),
    payload_ciphertext bytea,
    payload_nonce bytea,
    payload_tag bytea,
    encryption_key_reference text,
    payload_sha256 text NOT NULL CHECK (
        length(btrim(payload_sha256)) > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz,
    purged_at timestamptz,
    FOREIGN KEY (
        json_export_job_id,
        file_job_id,
        config_version_id,
        artifact_revision)
    REFERENCES json_export_jobs (
        id,
        file_job_id,
        config_version_id,
        artifact_revision)
    ON DELETE CASCADE,
    CHECK (
        (
            purged_at IS NULL
            AND payload_ciphertext IS NOT NULL
            AND octet_length(payload_ciphertext) > 0
            AND payload_nonce IS NOT NULL
            AND octet_length(payload_nonce) = 12
            AND payload_tag IS NOT NULL
            AND octet_length(payload_tag) = 16
            AND encryption_key_reference IS NOT NULL
            AND length(btrim(encryption_key_reference)) > 0
        ) OR (
            purged_at IS NOT NULL
            AND payload_ciphertext IS NULL
            AND payload_nonce IS NULL
            AND payload_tag IS NULL
            AND encryption_key_reference IS NULL
        )
    ),
    UNIQUE (
        file_job_id,
        config_version_id,
        artifact_revision,
        source_row_id,
        output_key)
);

CREATE INDEX ix_json_export_items_finalize_order
ON json_export_items (
    json_export_job_id,
    row_number,
    output_ordinal,
    output_key,
    id);

CREATE INDEX ix_json_export_items_row_reconcile
ON json_export_items (
    json_export_job_id,
    source_row_id,
    output_key);

CREATE INDEX ix_json_export_items_cleanup
ON json_export_items (expires_at, id)
WHERE expires_at IS NOT NULL
  AND purged_at IS NULL;

CREATE UNIQUE INDEX ux_outbox_json_export_finalize
ON outbox (topic, message_key)
WHERE topic = 'mapping.json-export-finalize';
