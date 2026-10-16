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
        data_type IN (
            'Text',
            'Date',
            'Decimal',
            'Boolean',
            'Integer',
            'DateTime',
            'Guid'
        )
    ),
    is_required boolean NOT NULL,
    ordinal integer NOT NULL,
    length integer,
    precision integer,
    scale integer,
    CHECK (length IS NULL OR (data_type = 'Text' AND length > 0)),
    CHECK (
        precision IS NULL OR (
            data_type = 'Decimal'
            AND precision BETWEEN 1 AND 1000
        )
    ),
    CHECK (
        scale IS NULL OR (
            data_type = 'Decimal'
            AND precision IS NOT NULL
            AND scale BETWEEN 0 AND precision
        )
    ),
    UNIQUE (table_id, name)
);
