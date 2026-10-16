ALTER TABLE row_jobs
ADD COLUMN evaluation_at timestamptz NOT NULL DEFAULT now();
