-- A compatible addition: allow the restricted runtime role to report the schema release.
UPDATE public.golfsg_database_release
SET version = '0.2.0-alpha', applied_at = CURRENT_TIMESTAMP
WHERE singleton = TRUE;
