ALTER TABLE mapping_config_versions
ADD COLUMN accepted_extensions jsonb NOT NULL
    DEFAULT '[".txt", ".csv"]'::jsonb;

ALTER TABLE mapping_config_versions
ADD CONSTRAINT mapping_config_versions_accepted_extensions_check
CHECK (
    jsonb_typeof(accepted_extensions) = 'array'
    AND jsonb_array_length(accepted_extensions) > 0
);
