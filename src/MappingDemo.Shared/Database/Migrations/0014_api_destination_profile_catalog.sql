CREATE TABLE api_destination_profiles (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    profile_key text NOT NULL UNIQUE CHECK (
        profile_key ~ '^[a-z][a-z0-9-]{1,62}$'),
    display_name text NOT NULL CHECK (length(btrim(display_name)) > 0),
    active_version_id bigint,
    concurrency_token bigint NOT NULL DEFAULT 1 CHECK (concurrency_token > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by text NOT NULL CHECK (length(btrim(created_by)) > 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by text NOT NULL CHECK (length(btrim(updated_by)) > 0)
);

CREATE TABLE api_destination_profile_versions (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    profile_id bigint NOT NULL REFERENCES api_destination_profiles(id),
    version_no integer NOT NULL CHECK (version_no > 0),
    lifecycle text NOT NULL CHECK (
        lifecycle IN ('Draft', 'Active', 'Disabled')),
    environment text NOT NULL CHECK (
        environment IN ('Stub', 'Sandbox', 'Production')),
    schema_base_url text NOT NULL CHECK (length(btrim(schema_base_url)) > 0),
    schema_path_template text NOT NULL CHECK (
        length(btrim(schema_path_template)) > 0),
    delivery_base_url text NOT NULL CHECK (
        length(btrim(delivery_base_url)) > 0),
    delivery_path_template text NOT NULL CHECK (
        length(btrim(delivery_path_template)) > 0),
    method text NOT NULL CHECK (method IN ('POST', 'PUT', 'PATCH')),
    auth_scheme text NOT NULL CHECK (
        auth_scheme IN ('None', 'Mtls', 'OAuth2ClientCredentials')),
    auth_secret_reference text,
    network_policy_reference text NOT NULL CHECK (
        length(btrim(network_policy_reference)) > 0),
    timeout_seconds integer NOT NULL CHECK (timeout_seconds BETWEEN 1 AND 120),
    idempotency_header_name text NOT NULL CHECK (
        length(btrim(idempotency_header_name)) > 0),
    correlation_header_name text NOT NULL CHECK (
        length(btrim(correlation_header_name)) > 0),
    allowed_header_names text[] NOT NULL CHECK (
        cardinality(allowed_header_names) > 0
        AND array_position(allowed_header_names, NULL) IS NULL),
    content_hash text NOT NULL CHECK (
        content_hash ~ '^sha256:[0-9a-f]{64}$'),
    concurrency_token bigint NOT NULL DEFAULT 1 CHECK (concurrency_token > 0),
    validated_at timestamptz,
    validated_content_hash text CHECK (
        validated_content_hash IS NULL
        OR validated_content_hash ~ '^sha256:[0-9a-f]{64}$'),
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by text NOT NULL CHECK (length(btrim(created_by)) > 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by text NOT NULL CHECK (length(btrim(updated_by)) > 0),
    activated_at timestamptz,
    activated_by text,
    disabled_at timestamptz,
    disabled_by text,
    CONSTRAINT api_destination_profile_versions_identity_unique
        UNIQUE (profile_id, version_no),
    CONSTRAINT api_destination_profile_versions_id_profile_unique
        UNIQUE (id, profile_id),
    CONSTRAINT api_destination_profile_versions_auth_check CHECK (
        (auth_scheme = 'None'
            AND NULLIF(btrim(auth_secret_reference), '') IS NULL)
        OR
        (auth_scheme <> 'None'
            AND auth_secret_reference IS NOT NULL
            AND length(btrim(auth_secret_reference)) > 0)),
    CONSTRAINT api_destination_profile_versions_validation_check CHECK (
        (validated_at IS NULL AND validated_content_hash IS NULL)
        OR
        (validated_at IS NOT NULL AND validated_content_hash IS NOT NULL)),
    CONSTRAINT api_destination_profile_versions_lifecycle_metadata_check CHECK (
        (lifecycle = 'Draft'
            AND activated_at IS NULL
            AND activated_by IS NULL
            AND disabled_at IS NULL
            AND disabled_by IS NULL)
        OR
        (lifecycle = 'Active'
            AND activated_at IS NOT NULL
            AND activated_by IS NOT NULL
            AND length(btrim(activated_by)) > 0
            AND disabled_at IS NULL
            AND disabled_by IS NULL
            AND validated_at IS NOT NULL
            AND validated_content_hash = content_hash)
        OR
        (lifecycle = 'Disabled'
            AND activated_at IS NOT NULL
            AND activated_by IS NOT NULL
            AND length(btrim(activated_by)) > 0
            AND disabled_at IS NOT NULL
            AND disabled_by IS NOT NULL
            AND length(btrim(disabled_by)) > 0))
);

ALTER TABLE api_destination_profiles
ADD CONSTRAINT api_destination_profiles_active_version_same_profile_fk
FOREIGN KEY (active_version_id, id)
REFERENCES api_destination_profile_versions(id, profile_id)
DEFERRABLE INITIALLY DEFERRED;

CREATE UNIQUE INDEX ux_api_destination_profile_versions_one_draft
ON api_destination_profile_versions (profile_id)
WHERE lifecycle = 'Draft';

CREATE UNIQUE INDEX ux_api_destination_profile_versions_one_active
ON api_destination_profile_versions (profile_id)
WHERE lifecycle = 'Active';

CREATE TABLE api_destination_profile_contracts (
    profile_version_id bigint NOT NULL
        REFERENCES api_destination_profile_versions(id),
    ordinal integer NOT NULL CHECK (ordinal > 0),
    contract_id text NOT NULL CHECK (length(btrim(contract_id)) > 0),
    contract_version text NOT NULL CHECK (
        length(btrim(contract_version)) > 0),
    contract_reference text NOT NULL CHECK (
        length(btrim(contract_reference)) > 0),
    PRIMARY KEY (profile_version_id, ordinal),
    UNIQUE (profile_version_id, contract_reference)
);

CREATE TABLE api_destination_profile_bindings (
    profile_version_id bigint NOT NULL
        REFERENCES api_destination_profile_versions(id),
    ordinal integer NOT NULL CHECK (ordinal > 0),
    target_path text NOT NULL CHECK (length(btrim(target_path)) > 0),
    runtime_value_source text NOT NULL CHECK (
        runtime_value_source IN (
            'ContractReference',
            'FileJobId',
            'FileName',
            'SourceRowId',
            'SourceRowNumber',
            'ConfigVersionReference',
            'OutputKey')),
    PRIMARY KEY (profile_version_id, ordinal),
    UNIQUE (profile_version_id, target_path)
);

CREATE TABLE api_destination_profile_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    profile_id bigint NOT NULL REFERENCES api_destination_profiles(id),
    profile_version_id bigint,
    action text NOT NULL CHECK (
        action IN (
            'CreateProfile',
            'UpdateDisplayName',
            'CreateDraft',
            'UpdateDraft',
            'ValidateDraft',
            'Activate',
            'Disable',
            'Rollback')),
    actor text NOT NULL CHECK (length(btrim(actor)) > 0),
    reason text NOT NULL CHECK (length(btrim(reason)) BETWEEN 10 AND 500),
    request_id text NOT NULL UNIQUE CHECK (length(btrim(request_id)) > 0),
    occurred_at timestamptz NOT NULL DEFAULT now(),
    before_lifecycle text CHECK (
        before_lifecycle IS NULL
        OR before_lifecycle IN ('Draft', 'Active', 'Disabled')),
    after_lifecycle text CHECK (
        after_lifecycle IS NULL
        OR after_lifecycle IN ('Draft', 'Active', 'Disabled')),
    before_content_hash text CHECK (
        before_content_hash IS NULL
        OR before_content_hash ~ '^sha256:[0-9a-f]{64}$'),
    after_content_hash text CHECK (
        after_content_hash IS NULL
        OR after_content_hash ~ '^sha256:[0-9a-f]{64}$'),
    concurrency_token bigint NOT NULL CHECK (concurrency_token > 0),
    CONSTRAINT api_destination_profile_audit_version_same_profile_fk
        FOREIGN KEY (profile_version_id, profile_id)
        REFERENCES api_destination_profile_versions(id, profile_id)
);

CREATE FUNCTION api_destination_profile_guard_identity()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'API Destination Profiles cannot be deleted'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.profile_key IS DISTINCT FROM OLD.profile_key THEN
        RAISE EXCEPTION 'API Destination Profile key is immutable'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END
$function$;

CREATE TRIGGER api_destination_profiles_identity_guard
BEFORE UPDATE OR DELETE ON api_destination_profiles
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_guard_identity();

CREATE FUNCTION api_destination_profile_guard_version()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'API Destination Profile Versions cannot be deleted'
            USING ERRCODE = '23514';
    END IF;

    IF TG_OP = 'INSERT' THEN
        IF NEW.lifecycle <> 'Draft' THEN
            RAISE EXCEPTION 'API Destination Profile Versions start as Draft'
                USING ERRCODE = '23514';
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.profile_id IS DISTINCT FROM OLD.profile_id
        OR NEW.version_no IS DISTINCT FROM OLD.version_no
        OR NEW.created_at IS DISTINCT FROM OLD.created_at
        OR NEW.created_by IS DISTINCT FROM OLD.created_by THEN
        RAISE EXCEPTION 'API Destination Profile Version identity is immutable'
            USING ERRCODE = '23514';
    END IF;

    IF NOT (
        NEW.lifecycle = OLD.lifecycle
        OR (OLD.lifecycle = 'Draft' AND NEW.lifecycle = 'Active')
        OR (OLD.lifecycle = 'Active' AND NEW.lifecycle = 'Disabled')
        OR (OLD.lifecycle = 'Disabled' AND NEW.lifecycle = 'Active')) THEN
        RAISE EXCEPTION 'Invalid API Destination Profile lifecycle transition'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.lifecycle <> 'Draft' AND (
        NEW.environment IS DISTINCT FROM OLD.environment
        OR NEW.schema_base_url IS DISTINCT FROM OLD.schema_base_url
        OR NEW.schema_path_template IS DISTINCT FROM OLD.schema_path_template
        OR NEW.delivery_base_url IS DISTINCT FROM OLD.delivery_base_url
        OR NEW.delivery_path_template IS DISTINCT FROM OLD.delivery_path_template
        OR NEW.method IS DISTINCT FROM OLD.method
        OR NEW.auth_scheme IS DISTINCT FROM OLD.auth_scheme
        OR NEW.auth_secret_reference IS DISTINCT FROM OLD.auth_secret_reference
        OR NEW.network_policy_reference IS DISTINCT FROM OLD.network_policy_reference
        OR NEW.timeout_seconds IS DISTINCT FROM OLD.timeout_seconds
        OR NEW.idempotency_header_name IS DISTINCT FROM OLD.idempotency_header_name
        OR NEW.correlation_header_name IS DISTINCT FROM OLD.correlation_header_name
        OR NEW.allowed_header_names IS DISTINCT FROM OLD.allowed_header_names
        OR NEW.content_hash IS DISTINCT FROM OLD.content_hash
        OR NEW.validated_at IS DISTINCT FROM OLD.validated_at
        OR NEW.validated_content_hash IS DISTINCT FROM OLD.validated_content_hash) THEN
        RAISE EXCEPTION 'Activated API Destination Profile content is immutable'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END
$function$;

CREATE TRIGGER api_destination_profile_versions_guard
BEFORE INSERT OR UPDATE OR DELETE ON api_destination_profile_versions
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_guard_version();

CREATE FUNCTION api_destination_profile_guard_child()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    version_id bigint;
    version_lifecycle text;
BEGIN
    version_id := CASE
        WHEN TG_OP = 'DELETE' THEN OLD.profile_version_id
        ELSE NEW.profile_version_id
    END;

    SELECT lifecycle
    INTO version_lifecycle
    FROM api_destination_profile_versions
    WHERE id = version_id;

    IF version_lifecycle IS DISTINCT FROM 'Draft' THEN
        RAISE EXCEPTION 'Activated API Destination Profile children are immutable'
            USING ERRCODE = '23514';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;

    RETURN NEW;
END
$function$;

CREATE TRIGGER api_destination_profile_contracts_guard
BEFORE INSERT OR UPDATE OR DELETE ON api_destination_profile_contracts
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_guard_child();

CREATE TRIGGER api_destination_profile_bindings_guard
BEFORE INSERT OR UPDATE OR DELETE ON api_destination_profile_bindings
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_guard_child();

CREATE FUNCTION api_destination_profile_guard_audit()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    RAISE EXCEPTION 'API Destination Profile audit is append-only'
        USING ERRCODE = '23514';
END
$function$;

CREATE TRIGGER api_destination_profile_audit_append_only
BEFORE UPDATE OR DELETE ON api_destination_profile_audit
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_guard_audit();

CREATE FUNCTION api_destination_profile_assert_active_pointer(
    checked_profile_id bigint)
RETURNS void
LANGUAGE plpgsql
AS $function$
DECLARE
    pointer_id bigint;
    active_count integer;
    active_id bigint;
BEGIN
    SELECT active_version_id
    INTO pointer_id
    FROM api_destination_profiles
    WHERE id = checked_profile_id;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    SELECT count(*), min(id)
    INTO active_count, active_id
    FROM api_destination_profile_versions
    WHERE profile_id = checked_profile_id
      AND lifecycle = 'Active';

    IF (pointer_id IS NULL AND active_count <> 0)
        OR (pointer_id IS NOT NULL
            AND (active_count <> 1 OR active_id <> pointer_id)) THEN
        RAISE EXCEPTION
            'API Destination Profile active pointer and lifecycle disagree'
            USING ERRCODE = '23514';
    END IF;
END
$function$;

CREATE FUNCTION api_destination_profile_check_profile_pointer()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    PERFORM api_destination_profile_assert_active_pointer(
        CASE WHEN TG_OP = 'DELETE' THEN OLD.id ELSE NEW.id END);
    RETURN NULL;
END
$function$;

CREATE FUNCTION api_destination_profile_check_version_pointer()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    PERFORM api_destination_profile_assert_active_pointer(
        CASE WHEN TG_OP = 'DELETE' THEN OLD.profile_id ELSE NEW.profile_id END);

    IF TG_OP = 'UPDATE' AND OLD.profile_id IS DISTINCT FROM NEW.profile_id THEN
        PERFORM api_destination_profile_assert_active_pointer(OLD.profile_id);
    END IF;

    RETURN NULL;
END
$function$;

CREATE CONSTRAINT TRIGGER api_destination_profiles_active_pointer_check
AFTER INSERT OR UPDATE OR DELETE ON api_destination_profiles
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_check_profile_pointer();

CREATE CONSTRAINT TRIGGER api_destination_profile_versions_active_pointer_check
AFTER INSERT OR UPDATE OR DELETE ON api_destination_profile_versions
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW
EXECUTE FUNCTION api_destination_profile_check_version_pointer();

ALTER TABLE mapping_config_versions
ADD COLUMN api_destination_profile_version_id bigint
    REFERENCES api_destination_profile_versions(id);

ALTER TABLE delivery_jobs
ADD COLUMN destination_profile_version_id bigint
    REFERENCES api_destination_profile_versions(id);

CREATE INDEX ix_mapping_config_versions_api_profile_version
ON mapping_config_versions (api_destination_profile_version_id)
WHERE api_destination_profile_version_id IS NOT NULL;

CREATE INDEX ix_delivery_jobs_profile_version
ON delivery_jobs (destination_profile_version_id)
WHERE destination_profile_version_id IS NOT NULL;
