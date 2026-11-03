CREATE TABLE contracts (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    contract_key text NOT NULL UNIQUE CHECK (
        contract_key ~ '^[a-z][a-z0-9-]{1,62}$'),
    display_name text NOT NULL CHECK (length(btrim(display_name)) > 0),
    concurrency_token bigint NOT NULL DEFAULT 1 CHECK (concurrency_token > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by text NOT NULL CHECK (length(btrim(created_by)) > 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by text NOT NULL CHECK (length(btrim(updated_by)) > 0)
);

CREATE TABLE contract_versions (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    contract_id bigint NOT NULL REFERENCES contracts(id),
    version text NOT NULL CHECK (
        version ~ '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$'),
    reference text NOT NULL UNIQUE CHECK (length(btrim(reference)) > 0),
    lifecycle text NOT NULL CHECK (
        lifecycle IN ('Draft', 'Published', 'Retired')),
    draft_schema text NOT NULL CHECK (
        octet_length(draft_schema) BETWEEN 2 AND 1048576),
    canonical_schema text,
    schema_hash text CHECK (
        schema_hash IS NULL OR schema_hash ~ '^sha256:[0-9a-f]{64}$'),
    compatibility text CHECK (
        compatibility IS NULL
        OR compatibility IN ('Initial', 'NonBreaking', 'Breaking')),
    compatibility_base_version_id bigint REFERENCES contract_versions(id),
    concurrency_token bigint NOT NULL DEFAULT 1 CHECK (concurrency_token > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by text NOT NULL CHECK (length(btrim(created_by)) > 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by text NOT NULL CHECK (length(btrim(updated_by)) > 0),
    published_at timestamptz,
    published_by text,
    retired_at timestamptz,
    retired_by text,
    CONSTRAINT contract_versions_identity_unique
        UNIQUE (contract_id, version),
    CONSTRAINT contract_versions_id_contract_unique
        UNIQUE (id, contract_id),
    CONSTRAINT contract_versions_lifecycle_metadata_check CHECK (
        (lifecycle = 'Draft'
            AND canonical_schema IS NULL
            AND schema_hash IS NULL
            AND compatibility IS NULL
            AND compatibility_base_version_id IS NULL
            AND published_at IS NULL
            AND published_by IS NULL
            AND retired_at IS NULL
            AND retired_by IS NULL)
        OR
        (lifecycle = 'Published'
            AND canonical_schema IS NOT NULL
            AND schema_hash IS NOT NULL
            AND compatibility IS NOT NULL
            AND published_at IS NOT NULL
            AND published_by IS NOT NULL
            AND length(btrim(published_by)) > 0
            AND retired_at IS NULL
            AND retired_by IS NULL)
        OR
        (lifecycle = 'Retired'
            AND canonical_schema IS NOT NULL
            AND schema_hash IS NOT NULL
            AND compatibility IS NOT NULL
            AND published_at IS NOT NULL
            AND published_by IS NOT NULL
            AND length(btrim(published_by)) > 0
            AND retired_at IS NOT NULL
            AND retired_by IS NOT NULL
            AND length(btrim(retired_by)) > 0))
);

CREATE UNIQUE INDEX ux_contract_versions_one_draft
ON contract_versions (contract_id)
WHERE lifecycle = 'Draft';

CREATE INDEX ix_contract_versions_published
ON contract_versions (contract_id, version)
WHERE lifecycle = 'Published';

CREATE TABLE contract_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    contract_id bigint NOT NULL REFERENCES contracts(id),
    contract_version_id bigint,
    action text NOT NULL CHECK (
        action IN (
            'CreateContract',
            'UpdateDisplayName',
            'CreateDraft',
            'UpdateDraft',
            'CloneVersion',
            'Publish',
            'Retire')),
    actor text NOT NULL CHECK (length(btrim(actor)) > 0),
    reason text NOT NULL CHECK (length(btrim(reason)) BETWEEN 10 AND 500),
    request_id text NOT NULL UNIQUE CHECK (length(btrim(request_id)) > 0),
    occurred_at timestamptz NOT NULL DEFAULT now(),
    before_lifecycle text CHECK (
        before_lifecycle IS NULL
        OR before_lifecycle IN ('Draft', 'Published', 'Retired')),
    after_lifecycle text CHECK (
        after_lifecycle IS NULL
        OR after_lifecycle IN ('Draft', 'Published', 'Retired')),
    before_schema_hash text CHECK (
        before_schema_hash IS NULL
        OR before_schema_hash ~ '^sha256:[0-9a-f]{64}$'),
    after_schema_hash text CHECK (
        after_schema_hash IS NULL
        OR after_schema_hash ~ '^sha256:[0-9a-f]{64}$'),
    concurrency_token bigint NOT NULL CHECK (concurrency_token > 0),
    CONSTRAINT contract_audit_version_same_contract_fk
        FOREIGN KEY (contract_version_id, contract_id)
        REFERENCES contract_versions(id, contract_id)
);

CREATE FUNCTION contract_guard_identity()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Contracts cannot be deleted'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.contract_key IS DISTINCT FROM OLD.contract_key
        OR NEW.created_at IS DISTINCT FROM OLD.created_at
        OR NEW.created_by IS DISTINCT FROM OLD.created_by THEN
        RAISE EXCEPTION 'Contract identity is immutable'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END
$function$;

CREATE TRIGGER contracts_identity_guard
BEFORE UPDATE OR DELETE ON contracts
FOR EACH ROW
EXECUTE FUNCTION contract_guard_identity();

CREATE FUNCTION contract_version_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    parent_contract_key text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Contract Versions cannot be deleted'
            USING ERRCODE = '23514';
    END IF;

    IF TG_OP = 'INSERT' THEN
        IF NEW.lifecycle <> 'Draft' THEN
            RAISE EXCEPTION 'Contract Versions start as Draft'
                USING ERRCODE = '23514';
        END IF;
        SELECT contract_key
        INTO parent_contract_key
        FROM contracts
        WHERE id = NEW.contract_id;
        IF NEW.reference IS DISTINCT FROM
            parent_contract_key || '/' || NEW.version THEN
            RAISE EXCEPTION 'Contract Version reference must be derived from identity'
                USING ERRCODE = '23514';
        END IF;
        RETURN NEW;
    END IF;

    IF NEW.contract_id IS DISTINCT FROM OLD.contract_id
        OR NEW.version IS DISTINCT FROM OLD.version
        OR NEW.reference IS DISTINCT FROM OLD.reference
        OR NEW.created_at IS DISTINCT FROM OLD.created_at
        OR NEW.created_by IS DISTINCT FROM OLD.created_by THEN
        RAISE EXCEPTION 'Contract Version identity is immutable'
            USING ERRCODE = '23514';
    END IF;

    IF NOT (
        NEW.lifecycle = OLD.lifecycle
        OR (OLD.lifecycle = 'Draft' AND NEW.lifecycle = 'Published')
        OR (OLD.lifecycle = 'Published' AND NEW.lifecycle = 'Retired')) THEN
        RAISE EXCEPTION 'Invalid Contract Version lifecycle transition'
            USING ERRCODE = '23514';
    END IF;

    IF OLD.lifecycle <> 'Draft' AND (
        NEW.draft_schema IS DISTINCT FROM OLD.draft_schema
        OR NEW.canonical_schema IS DISTINCT FROM OLD.canonical_schema
        OR NEW.schema_hash IS DISTINCT FROM OLD.schema_hash
        OR NEW.compatibility IS DISTINCT FROM OLD.compatibility
        OR NEW.compatibility_base_version_id IS DISTINCT FROM
            OLD.compatibility_base_version_id
        OR NEW.published_at IS DISTINCT FROM OLD.published_at
        OR NEW.published_by IS DISTINCT FROM OLD.published_by) THEN
        RAISE EXCEPTION 'Published Contract Version content is immutable'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END
$function$;

CREATE TRIGGER contract_versions_guard
BEFORE INSERT OR UPDATE OR DELETE ON contract_versions
FOR EACH ROW
EXECUTE FUNCTION contract_version_guard();

CREATE FUNCTION contract_audit_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    RAISE EXCEPTION 'Contract audit is append-only'
        USING ERRCODE = '23514';
END
$function$;

CREATE TRIGGER contract_audit_append_only
BEFORE UPDATE OR DELETE ON contract_audit
FOR EACH ROW
EXECUTE FUNCTION contract_audit_guard();

ALTER TABLE api_destination_profile_contracts
ADD COLUMN contract_version_id bigint REFERENCES contract_versions(id);

CREATE INDEX ix_api_destination_profile_contract_version
ON api_destination_profile_contracts (contract_version_id)
WHERE contract_version_id IS NOT NULL;

DROP TRIGGER api_destination_profile_contracts_guard
ON api_destination_profile_contracts;

CREATE FUNCTION api_destination_profile_contract_registry_child_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    version_lifecycle text;
BEGIN
    SELECT lifecycle
    INTO version_lifecycle
    FROM api_destination_profile_versions
    WHERE id = CASE
        WHEN TG_OP = 'DELETE' THEN OLD.profile_version_id
        ELSE NEW.profile_version_id
    END;

    IF version_lifecycle = 'Draft' THEN
        IF TG_OP = 'DELETE' THEN
            RETURN OLD;
        END IF;
        RETURN NEW;
    END IF;

    IF TG_OP = 'UPDATE'
        AND OLD.contract_version_id IS NULL
        AND NEW.contract_version_id IS NOT NULL
        AND NEW.profile_version_id IS NOT DISTINCT FROM OLD.profile_version_id
        AND NEW.ordinal IS NOT DISTINCT FROM OLD.ordinal
        AND NEW.contract_id IS NOT DISTINCT FROM OLD.contract_id
        AND NEW.contract_version IS NOT DISTINCT FROM OLD.contract_version
        AND NEW.contract_reference IS NOT DISTINCT FROM OLD.contract_reference THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION
        'Activated API Destination Profile contract content is immutable'
        USING ERRCODE = '23514';
END
$function$;

CREATE TRIGGER api_destination_profile_contracts_guard
BEFORE INSERT OR UPDATE OR DELETE ON api_destination_profile_contracts
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_contract_registry_child_guard();

CREATE FUNCTION api_destination_profile_contract_registry_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    linked_contract_key text;
    linked_version text;
    linked_reference text;
    linked_lifecycle text;
BEGIN
    IF NEW.contract_version_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT c.contract_key, v.version, v.reference, v.lifecycle
    INTO linked_contract_key, linked_version, linked_reference,
        linked_lifecycle
    FROM contract_versions v
    INNER JOIN contracts c ON c.id = v.contract_id
    WHERE v.id = NEW.contract_version_id;

    IF linked_lifecycle IS DISTINCT FROM 'Published'
        OR NEW.contract_id IS DISTINCT FROM linked_contract_key
        OR NEW.contract_version IS DISTINCT FROM linked_version
        OR NEW.contract_reference IS DISTINCT FROM linked_reference THEN
        RAISE EXCEPTION
            'Profile contract identity must match a Published Contract Version'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END
$function$;

CREATE TRIGGER api_destination_profile_contract_registry_check
BEFORE INSERT OR UPDATE ON api_destination_profile_contracts
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_contract_registry_guard();
