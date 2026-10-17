-- Initial explicit schema release. Migrations 001-003 remain unchanged.
-- The setup tool applies and records this migration in the same transaction
-- as all preceding migrations, so a failed setup cannot advance the version.
CREATE TABLE public.golfsg_database_release (
    singleton BOOLEAN PRIMARY KEY DEFAULT TRUE CHECK (singleton),
    version TEXT NOT NULL,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO public.golfsg_database_release (version) VALUES ('0.1.0-alpha');
