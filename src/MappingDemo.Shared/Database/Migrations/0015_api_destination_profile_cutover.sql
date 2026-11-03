CREATE FUNCTION seed_api_destination_profile(
    seed_profile_key text,
    seed_display_name text,
    seed_environment text,
    seed_schema_base_url text,
    seed_schema_path_template text,
    seed_delivery_base_url text,
    seed_delivery_path_template text,
    seed_method text,
    seed_auth_scheme text,
    seed_auth_secret_reference text,
    seed_timeout_seconds integer,
    seed_idempotency_header_name text,
    seed_correlation_header_name text,
    seed_allowed_header_names text[],
    seed_content_hash text,
    seed_contracts jsonb,
    seed_bindings jsonb)
RETURNS void
LANGUAGE plpgsql
AS $function$
DECLARE
    seeded_profile_id bigint;
    seeded_version_id bigint;
    existing_content_hash text;
BEGIN
    INSERT INTO api_destination_profiles (
        profile_key,
        display_name,
        created_by,
        updated_by)
    VALUES (
        seed_profile_key,
        seed_display_name,
        'phase-26.5-bootstrap',
        'phase-26.5-bootstrap')
    ON CONFLICT (profile_key) DO NOTHING;

    SELECT id, active_version_id
    INTO seeded_profile_id, seeded_version_id
    FROM api_destination_profiles
    WHERE profile_key = seed_profile_key
    FOR UPDATE;

    IF seeded_version_id IS NOT NULL THEN
        SELECT content_hash
        INTO existing_content_hash
        FROM api_destination_profile_versions
        WHERE id = seeded_version_id;

        IF existing_content_hash IS DISTINCT FROM seed_content_hash THEN
            RAISE EXCEPTION
                'API Destination Profile % does not match bootstrap content',
                seed_profile_key
                USING ERRCODE = '23514';
        END IF;
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM api_destination_profile_versions
        WHERE profile_id = seeded_profile_id) THEN
        RAISE EXCEPTION
            'API Destination Profile % has no unambiguous active bootstrap version',
            seed_profile_key
            USING ERRCODE = '23514';
    END IF;

    INSERT INTO api_destination_profile_versions (
        profile_id,
        version_no,
        lifecycle,
        environment,
        schema_base_url,
        schema_path_template,
        delivery_base_url,
        delivery_path_template,
        method,
        auth_scheme,
        auth_secret_reference,
        network_policy_reference,
        timeout_seconds,
        idempotency_header_name,
        correlation_header_name,
        allowed_header_names,
        content_hash,
        created_by,
        updated_by)
    VALUES (
        seeded_profile_id,
        1,
        'Draft',
        seed_environment,
        seed_schema_base_url,
        seed_schema_path_template,
        seed_delivery_base_url,
        seed_delivery_path_template,
        seed_method,
        seed_auth_scheme,
        seed_auth_secret_reference,
        'partner-egress-default',
        seed_timeout_seconds,
        seed_idempotency_header_name,
        seed_correlation_header_name,
        seed_allowed_header_names,
        seed_content_hash,
        'phase-26.5-bootstrap',
        'phase-26.5-bootstrap')
    RETURNING id INTO seeded_version_id;

    INSERT INTO api_destination_profile_contracts (
        profile_version_id,
        ordinal,
        contract_id,
        contract_version,
        contract_reference)
    SELECT
        seeded_version_id,
        item.ordinality::integer,
        item.value->>'id',
        item.value->>'version',
        item.value->>'reference'
    FROM jsonb_array_elements(seed_contracts)
        WITH ORDINALITY AS item(value, ordinality);

    INSERT INTO api_destination_profile_bindings (
        profile_version_id,
        ordinal,
        target_path,
        runtime_value_source)
    SELECT
        seeded_version_id,
        item.ordinality::integer,
        item.value->>'targetPath',
        item.value->>'source'
    FROM jsonb_array_elements(seed_bindings)
        WITH ORDINALITY AS item(value, ordinality);

    UPDATE api_destination_profile_versions
    SET validated_at = now(),
        validated_content_hash = content_hash
    WHERE id = seeded_version_id;

    UPDATE api_destination_profile_versions
    SET lifecycle = 'Active',
        activated_at = now(),
        activated_by = 'phase-26.5-bootstrap'
    WHERE id = seeded_version_id;

    UPDATE api_destination_profiles
    SET active_version_id = seeded_version_id,
        updated_at = now(),
        updated_by = 'phase-26.5-bootstrap'
    WHERE id = seeded_profile_id;

    INSERT INTO api_destination_profile_audit (
        profile_id,
        profile_version_id,
        action,
        actor,
        reason,
        request_id,
        after_lifecycle,
        after_content_hash,
        concurrency_token)
    VALUES (
        seeded_profile_id,
        seeded_version_id,
        'Activate',
        'phase-26.5-bootstrap',
        'Seed approved profile for Phase 26.5 cutover',
        'phase-26.5-seed-' || seed_profile_key,
        'Active',
        seed_content_hash,
        1);
END
$function$;

SELECT seed_api_destination_profile(
    'pa-mtls',
    'PA MTLS Proposal',
    'Sandbox',
    'https://contracts.mapping-demo.invalid',
    '/schemas/{contractReference}',
    'https://delivery.mapping-demo.invalid',
    '/proposals/{contractVersion}',
    'POST',
    'Mtls',
    'pa-mtls-client-certificate',
    10,
    'Idempotency-Key',
    'X-Correlation-Id',
    ARRAY['X-Correlation-Id', 'Idempotency-Key'],
    'sha256:2a91d41deb9669028707351bf45ae22c48b602f08a9113d84980de56d38a0d1e',
    '[{"id":"proposal","version":"1","reference":"proposal/1"}]'::jsonb,
    '[{"targetPath":"envelope.contractReference","source":"ContractReference"},{"targetPath":"envelope.fileJobId","source":"FileJobId"},{"targetPath":"envelope.sourceRowId","source":"SourceRowId"},{"targetPath":"envelope.sourceRowNumber","source":"SourceRowNumber"},{"targetPath":"envelope.configVersionReference","source":"ConfigVersionReference"},{"targetPath":"envelope.outputKey","source":"OutputKey"}]'::jsonb);

SELECT seed_api_destination_profile(
    'pa-mtls-inform',
    'PA MTLS Inform',
    'Sandbox',
    'https://contracts.mapping-demo.invalid',
    '/schemas/{contractReference}',
    'https://delivery.mapping-demo.invalid',
    '/informs/{contractVersion}',
    'POST',
    'Mtls',
    'pa-mtls-client-certificate',
    10,
    'Idempotency-Key',
    'X-Correlation-Id',
    ARRAY['X-Correlation-Id', 'Idempotency-Key'],
    'sha256:4767534ee2a00f198ac38eb2a06fe052aeca2bc94cf194dbd5c796998bfd8ecf',
    '[{"id":"pa-inform","version":"1","reference":"pa-inform/1"}]'::jsonb,
    '[{"targetPath":"contractVersion","source":"ContractReference"},{"targetPath":"fileJobId","source":"FileJobId"},{"targetPath":"fileName","source":"FileName"},{"targetPath":"sourceRowId","source":"SourceRowId"},{"targetPath":"outputKey","source":"OutputKey"},{"targetPath":"sourceRowNo","source":"SourceRowNumber"},{"targetPath":"mappingConfigVersionId","source":"ConfigVersionReference"}]'::jsonb);

SELECT seed_api_destination_profile(
    'claims-envelope',
    'Claims Envelope v2',
    'Sandbox',
    'https://claims-contracts.mapping-demo.invalid',
    '/contracts/{contractReference}',
    'https://claims-delivery.mapping-demo.invalid',
    '/cases/{contractVersion}',
    'PUT',
    'OAuth2ClientCredentials',
    'claims-oauth-client',
    15,
    'Idempotency-Key',
    'X-Request-Id',
    ARRAY['X-Request-Id', 'Idempotency-Key'],
    'sha256:e0d09dceb0095139174d41ba799f8ef9cdcf90f97a31cc3ef98f4e38d40da6c6',
    '[{"id":"claims","version":"2","reference":"claims/2"}]'::jsonb,
    '[{"targetPath":"meta.contractKey","source":"ContractReference"},{"targetPath":"meta.batch","source":"FileJobId"},{"targetPath":"meta.record","source":"SourceRowId"},{"targetPath":"meta.line","source":"SourceRowNumber"},{"targetPath":"meta.revision","source":"ConfigVersionReference"},{"targetPath":"meta.branch","source":"OutputKey"}]'::jsonb);

DROP FUNCTION seed_api_destination_profile(
    text, text, text, text, text, text, text, text, text, text, integer,
    text, text, text[], text, jsonb, jsonb);

DO $function$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM mapping_config_versions AS versions
        LEFT JOIN api_destination_profiles AS profiles
            ON profiles.profile_key = versions.api_destination_profile_id
        WHERE versions.api_destination_profile_id IS NOT NULL
          AND versions.api_destination_profile_version_id IS NULL
          AND profiles.active_version_id IS NULL) THEN
        RAISE EXCEPTION
            'Mapping Config Version has an unmapped API Destination Profile key'
            USING ERRCODE = '23514';
    END IF;

    UPDATE mapping_config_versions AS versions
    SET api_destination_profile_version_id = profiles.active_version_id
    FROM api_destination_profiles AS profiles
    WHERE versions.api_destination_profile_id = profiles.profile_key
      AND versions.api_destination_profile_version_id IS NULL;

    IF EXISTS (
        SELECT 1
        FROM delivery_jobs AS jobs
        LEFT JOIN api_destination_profiles AS profiles
            ON profiles.profile_key = jobs.destination_profile_id
        WHERE jobs.destination_profile_id IS NOT NULL
          AND jobs.destination_profile_version_id IS NULL
          AND profiles.active_version_id IS NULL) THEN
        RAISE EXCEPTION
            'Delivery Job has an unmapped API Destination Profile key'
            USING ERRCODE = '23514';
    END IF;

    UPDATE delivery_jobs AS jobs
    SET destination_profile_version_id = profiles.active_version_id
    FROM api_destination_profiles AS profiles
    WHERE jobs.destination_profile_id = profiles.profile_key
      AND jobs.destination_profile_version_id IS NULL;

    IF EXISTS (
        SELECT 1
        FROM mapping_config_versions AS versions
        INNER JOIN api_destination_profile_versions AS profile_versions
            ON profile_versions.id =
                versions.api_destination_profile_version_id
        INNER JOIN api_destination_profiles AS profiles
            ON profiles.id = profile_versions.profile_id
        WHERE versions.api_destination_profile_id IS DISTINCT FROM
            profiles.profile_key) OR EXISTS (
        SELECT 1
        FROM delivery_jobs AS jobs
        INNER JOIN api_destination_profile_versions AS profile_versions
            ON profile_versions.id = jobs.destination_profile_version_id
        INNER JOIN api_destination_profiles AS profiles
            ON profiles.id = profile_versions.profile_id
        WHERE jobs.destination_profile_id IS DISTINCT FROM
            profiles.profile_key) THEN
        RAISE EXCEPTION
            'Pinned API Destination Profile identity does not match logical key'
            USING ERRCODE = '23514';
    END IF;
END
$function$;
