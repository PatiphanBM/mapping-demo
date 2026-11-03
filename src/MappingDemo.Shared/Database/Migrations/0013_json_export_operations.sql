ALTER TABLE delivery_runtime_control
ADD COLUMN json_file_activation_enabled boolean NOT NULL DEFAULT false,
ADD COLUMN json_file_finalizer_paused boolean NOT NULL DEFAULT true;

ALTER TABLE delivery_control_audit
ADD COLUMN json_file_activation_enabled boolean NOT NULL DEFAULT false,
ADD COLUMN json_file_finalizer_paused boolean NOT NULL DEFAULT true;

CREATE TABLE json_export_retry_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    json_export_job_id bigint NOT NULL REFERENCES json_export_jobs(id)
        ON DELETE CASCADE,
    requested_by text NOT NULL CHECK (length(btrim(requested_by)) > 0),
    reason text NOT NULL CHECK (length(btrim(reason)) BETWEEN 1 AND 500),
    requested_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE json_export_operation_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    config_id bigint NOT NULL REFERENCES mapping_configs(id),
    config_version_id bigint NOT NULL REFERENCES mapping_config_versions(id),
    destination_profile_id text NOT NULL CHECK (
        length(btrim(destination_profile_id)) > 0),
    event_type text NOT NULL CHECK (
        event_type IN ('HashConflict', 'CleanupFailure')),
    details_redacted text NOT NULL CHECK (
        length(btrim(details_redacted)) > 0),
    occurred_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_json_export_operation_events_metrics
ON json_export_operation_events (
    config_id,
    config_version_id,
    destination_profile_id,
    event_type,
    occurred_at);
