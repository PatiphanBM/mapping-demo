ALTER TABLE table_definitions
ADD COLUMN verification_status text NOT NULL DEFAULT 'Unknown',
ADD COLUMN verification_checked_at timestamptz,
ADD COLUMN verification_duration_ms double precision,
ADD CONSTRAINT table_definitions_verification_status_check
CHECK (verification_status IN (
    'Unknown',
    'Verifying',
    'Healthy',
    'Drifted',
    'Missing',
    'VerificationFailed'
));

CREATE TABLE table_schema_verifications (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    table_id bigint NOT NULL REFERENCES table_definitions(id),
    status text NOT NULL CHECK (status IN (
        'Healthy',
        'Drifted',
        'Missing',
        'VerificationFailed'
    )),
    expected_schema jsonb NOT NULL,
    actual_schema jsonb,
    diagnostics jsonb NOT NULL,
    duration_ms double precision NOT NULL CHECK (duration_ms >= 0),
    checked_at timestamptz NOT NULL,
    operator_id text,
    correlation_id text NOT NULL
);

CREATE INDEX ix_table_schema_verifications_latest
ON table_schema_verifications (table_id, checked_at DESC, id DESC);

CREATE TABLE table_admin_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    table_id bigint REFERENCES table_definitions(id),
    action text NOT NULL CHECK (action IN (
        'Create',
        'AddColumn',
        'Verify'
    )),
    outcome text NOT NULL CHECK (outcome IN ('Success', 'Failure')),
    operator_id text NOT NULL,
    reason text NOT NULL,
    correlation_id text NOT NULL,
    safe_detail jsonb NOT NULL DEFAULT '{}'::jsonb,
    occurred_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_table_admin_audit_table
ON table_admin_audit (table_id, occurred_at DESC, id DESC);
