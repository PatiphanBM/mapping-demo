CREATE TABLE mapping_configs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL UNIQUE,
    input_folder text NOT NULL UNIQUE,
    source_table_id bigint NOT NULL REFERENCES table_definitions(id),
    normalized_table_id bigint NOT NULL REFERENCES table_definitions(id),
    active_version_id bigint,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE mapping_config_versions (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    config_id bigint NOT NULL REFERENCES mapping_configs(id),
    version_no integer NOT NULL,
    file_to_source jsonb NOT NULL,
    source_to_normalized jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    activated_at timestamptz,
    UNIQUE (config_id, version_no)
);

ALTER TABLE mapping_configs
ADD CONSTRAINT mapping_configs_active_version_id_fkey
FOREIGN KEY (active_version_id)
REFERENCES mapping_config_versions(id);
