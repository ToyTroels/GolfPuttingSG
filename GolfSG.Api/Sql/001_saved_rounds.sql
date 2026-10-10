-- Run as postgres (or the database owner) in MauiAppDB, once before using round sync.
-- Safe to repeat. No existing products or round rows are removed.
BEGIN;

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

GRANT SELECT, INSERT, UPDATE ON public.saved_rounds TO mauiapp_user;

COMMIT;
