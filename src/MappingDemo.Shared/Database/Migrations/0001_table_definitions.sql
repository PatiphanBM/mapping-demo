CREATE TABLE table_definitions (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL UNIQUE,
    kind text NOT NULL CHECK (kind IN ('Source', 'Normalized')),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE table_columns (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    table_id bigint NOT NULL REFERENCES table_definitions(id),
    name text NOT NULL,
    data_type text NOT NULL CHECK (
        data_type IN ('Text', 'Date', 'Decimal', 'Boolean')
    ),
    is_required boolean NOT NULL,
    ordinal integer NOT NULL,
    UNIQUE (table_id, name)
);
