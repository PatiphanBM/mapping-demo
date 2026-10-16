ALTER TABLE row_errors
ADD COLUMN output_key text;

ALTER TABLE row_errors
ADD COLUMN expression_path text;

UPDATE row_errors
SET expression_path = field;

ALTER TABLE row_errors
ALTER COLUMN expression_path SET NOT NULL;

ALTER TABLE row_errors
ALTER COLUMN field DROP NOT NULL;
