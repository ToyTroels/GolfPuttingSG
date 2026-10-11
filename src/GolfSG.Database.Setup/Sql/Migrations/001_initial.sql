-- Adopt the existing local tables without deleting or replacing their data.
CREATE TABLE IF NOT EXISTS public.products (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.saved_rounds (
    id VARCHAR(100) PRIMARY KEY,
    version UUID NOT NULL,
    payload JSONB NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT saved_rounds_id_format CHECK (id ~ '^[A-Za-z0-9_-]+$'),
    CONSTRAINT saved_rounds_payload_id CHECK (
        payload IS NULL OR (jsonb_typeof(payload) = 'object' AND payload->>'id' IS NOT NULL AND payload->>'id' = id)
    )
);
