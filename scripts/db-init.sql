-- Postgres initialization for the shipping-orchestrator local stack.
-- Runs once when the postgres container is created (mounted into /docker-entrypoint-initdb.d).
-- Idempotent: safe to re-run if reused on an existing volume.

-- Schemas — EF migrations create their own tables inside, but the schemas need to exist
-- so the per-schema role grants resolve cleanly.
CREATE SCHEMA IF NOT EXISTS orchestrator;
CREATE SCHEMA IF NOT EXISTS messaging;
CREATE SCHEMA IF NOT EXISTS ops_read;
CREATE SCHEMA IF NOT EXISTS customer_read;

-- Per-schema roles — plan section "Per-schema Postgres roles". Production uses these to
-- guarantee PublicApi cannot reach the orchestrator schema; locally we still create them
-- so our tests can validate role-scoped connections later.
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'orchestrator_app') THEN
    CREATE ROLE orchestrator_app LOGIN PASSWORD 'app_dev_password';
  END IF;
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'ops_read_role') THEN
    CREATE ROLE ops_read_role LOGIN PASSWORD 'app_dev_password';
  END IF;
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'customer_read_role') THEN
    CREATE ROLE customer_read_role LOGIN PASSWORD 'app_dev_password';
  END IF;
END $$;

GRANT USAGE, CREATE ON SCHEMA orchestrator TO orchestrator_app;
GRANT USAGE, CREATE ON SCHEMA messaging    TO orchestrator_app;
GRANT USAGE, CREATE ON SCHEMA ops_read     TO orchestrator_app;
GRANT USAGE, CREATE ON SCHEMA customer_read TO orchestrator_app;

GRANT USAGE ON SCHEMA ops_read       TO ops_read_role;
GRANT USAGE ON SCHEMA customer_read  TO customer_read_role;

ALTER DEFAULT PRIVILEGES IN SCHEMA ops_read
  GRANT SELECT ON TABLES TO ops_read_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA customer_read
  GRANT SELECT ON TABLES TO customer_read_role;
