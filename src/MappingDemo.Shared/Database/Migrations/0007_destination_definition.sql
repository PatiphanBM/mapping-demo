ALTER TABLE mapping_config_versions
ADD COLUMN destination_definition jsonb,
ADD COLUMN api_destination_profile_id text,
ADD COLUMN contract_reference text,
ADD COLUMN contract_schema_snapshot jsonb,
ADD COLUMN contract_schema_hash text,
ADD COLUMN contract_schema_fetched_at timestamptz,
ADD COLUMN payload_bindings_snapshot jsonb;

UPDATE mapping_config_versions AS versions
SET destination_definition = jsonb_build_object(
    'kind', 'legacyTable',
    'normalizedTableId', configs.normalized_table_id)
FROM mapping_configs AS configs
WHERE configs.id = versions.config_id
  AND versions.destination_definition IS NULL
  AND configs.normalized_table_id IS NOT NULL;

ALTER TABLE mapping_config_versions
ADD CONSTRAINT mapping_config_versions_destination_kind_check
CHECK (
    destination_definition IS NULL OR (
        jsonb_typeof(destination_definition) = 'object'
        AND destination_definition ? 'kind'
        AND jsonb_typeof(destination_definition -> 'kind') = 'string'
        AND destination_definition ->> 'kind' IN (
            'legacyTable',
            'apiContract'
        )
    )
);

CREATE INDEX ix_mapping_config_versions_destination_kind
ON mapping_config_versions ((destination_definition ->> 'kind'))
WHERE destination_definition IS NOT NULL;

ALTER TABLE mapping_configs
ALTER COLUMN normalized_table_id DROP NOT NULL;
