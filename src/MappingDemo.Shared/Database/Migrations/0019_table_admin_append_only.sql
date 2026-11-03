CREATE OR REPLACE FUNCTION table_admin_guard_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION '% is append-only', TG_TABLE_NAME
        USING ERRCODE = '23514';
END
$$;

CREATE TRIGGER table_schema_verifications_append_only
BEFORE UPDATE OR DELETE ON table_schema_verifications
FOR EACH ROW
EXECUTE FUNCTION table_admin_guard_history();

CREATE TRIGGER table_admin_audit_append_only
BEFORE UPDATE OR DELETE ON table_admin_audit
FOR EACH ROW
EXECUTE FUNCTION table_admin_guard_history();
