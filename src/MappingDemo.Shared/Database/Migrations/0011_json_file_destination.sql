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
            'jsonFile'
        )
    )
);
