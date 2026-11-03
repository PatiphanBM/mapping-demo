CREATE OR REPLACE FUNCTION api_destination_profile_contract_registry_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    linked_contract_key text;
    linked_version text;
    linked_reference text;
    linked_lifecycle text;
BEGIN
    IF NEW.contract_version_id IS NULL AND TG_OP = 'INSERT' THEN
        SELECT v.id
        INTO NEW.contract_version_id
        FROM contract_versions v
        INNER JOIN contracts c ON c.id = v.contract_id
        WHERE c.contract_key = NEW.contract_id
          AND v.version = NEW.contract_version
          AND v.reference = NEW.contract_reference
          AND v.lifecycle = 'Published';
    END IF;

    IF NEW.contract_version_id IS NULL THEN
        IF TG_OP = 'UPDATE' THEN
            RETURN NEW;
        END IF;
        RAISE EXCEPTION
            'Profile contract must reference a Published Contract Version'
            USING ERRCODE = '23514';
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
