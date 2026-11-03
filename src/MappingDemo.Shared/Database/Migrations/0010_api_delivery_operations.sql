CREATE TABLE delivery_runtime_control (
    singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
    delivery_paused boolean NOT NULL DEFAULT false,
    api_contract_activation_enabled boolean NOT NULL DEFAULT true,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by text NOT NULL DEFAULT 'migration',
    reason text NOT NULL DEFAULT 'Initial Phase 24 control state'
);

INSERT INTO delivery_runtime_control (singleton)
VALUES (true)
ON CONFLICT (singleton) DO NOTHING;

CREATE TABLE delivery_control_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    delivery_paused boolean NOT NULL,
    api_contract_activation_enabled boolean NOT NULL,
    changed_by text NOT NULL,
    reason text NOT NULL,
    changed_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE api_delivery_rollouts (
    config_id bigint PRIMARY KEY REFERENCES mapping_configs(id),
    stage text NOT NULL CHECK (stage IN ('Shadow', 'Canary', 'Expanded')),
    enabled boolean NOT NULL DEFAULT false,
    approved_by text NOT NULL,
    reason text NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE api_delivery_rollout_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    config_id bigint NOT NULL REFERENCES mapping_configs(id),
    stage text NOT NULL CHECK (stage IN ('Shadow', 'Canary', 'Expanded')),
    enabled boolean NOT NULL,
    changed_by text NOT NULL,
    reason text NOT NULL,
    changed_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_delivery_jobs_config_contract_status
ON delivery_jobs (config_version_id, contract_reference, status);

CREATE INDEX ix_delivery_attempts_finished_outcome
ON delivery_attempts (finished_at, outcome);

ALTER TABLE delivery_jobs ALTER COLUMN payload_ciphertext DROP NOT NULL;
ALTER TABLE delivery_jobs ALTER COLUMN payload_nonce DROP NOT NULL;
ALTER TABLE delivery_jobs ALTER COLUMN payload_tag DROP NOT NULL;

ALTER TABLE delivery_jobs
    DROP CONSTRAINT delivery_jobs_resend_of_delivery_job_id_fkey,
    ADD CONSTRAINT delivery_jobs_resend_of_delivery_job_id_fkey
        FOREIGN KEY (resend_of_delivery_job_id)
        REFERENCES delivery_jobs(id)
        ON DELETE SET NULL;

ALTER TABLE delivery_resend_audit
    DROP CONSTRAINT delivery_resend_audit_original_delivery_job_id_fkey,
    ADD CONSTRAINT delivery_resend_audit_original_delivery_job_id_fkey
        FOREIGN KEY (original_delivery_job_id)
        REFERENCES delivery_jobs(id)
        ON DELETE CASCADE,
    DROP CONSTRAINT delivery_resend_audit_resend_delivery_job_id_fkey,
    ADD CONSTRAINT delivery_resend_audit_resend_delivery_job_id_fkey
        FOREIGN KEY (resend_delivery_job_id)
        REFERENCES delivery_jobs(id)
        ON DELETE CASCADE;
