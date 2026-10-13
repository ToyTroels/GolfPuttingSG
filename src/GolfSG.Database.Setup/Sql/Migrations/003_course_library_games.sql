CREATE TABLE public.course_images (
    id VARCHAR(64) PRIMARY KEY,
    bytes BYTEA NOT NULL CHECK (octet_length(bytes) BETWEEN 8 AND 10485760)
);
CREATE TABLE public.shared_courses (
    id UUID PRIMARY KEY,
    owner_id TEXT NOT NULL REFERENCES public."AspNetUsers"("Id"),
    creator TEXT NOT NULL,
    source_id VARCHAR(100) NOT NULL,
    revision_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    positions INTEGER NOT NULL CHECK (positions BETWEEN 1 AND 100),
    attempts INTEGER NOT NULL CHECK (attempts BETWEEN 1 AND 1000),
    published BOOLEAN NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    UNIQUE (owner_id, source_id)
);
CREATE TABLE public.course_revisions (
    id UUID PRIMARY KEY,
    course_id UUID NOT NULL REFERENCES public.shared_courses(id),
    content_hash VARCHAR(64) NOT NULL,
    definition JSONB NOT NULL CHECK (jsonb_typeof(definition) = 'object'),
    image_id VARCHAR(64) NOT NULL REFERENCES public.course_images(id),
    UNIQUE (course_id, content_hash),
    UNIQUE (course_id, id)
);
ALTER TABLE public.shared_courses ADD CONSTRAINT shared_course_current_revision
    FOREIGN KEY (id, revision_id) REFERENCES public.course_revisions(course_id, id)
    DEFERRABLE INITIALLY DEFERRED;
CREATE TABLE public.course_games (
    id UUID PRIMARY KEY,
    host_id TEXT NOT NULL REFERENCES public."AspNetUsers"("Id"),
    host_name TEXT NOT NULL,
    name VARCHAR(100) NOT NULL,
    code VARCHAR(6) NOT NULL UNIQUE CHECK (code ~ '^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$'),
    definition JSONB NOT NULL CHECK (jsonb_typeof(definition) = 'object'),
    image_id VARCHAR(64) NOT NULL REFERENCES public.course_images(id),
    scoring_version TEXT NOT NULL,
    closed BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX course_games_host ON public.course_games(host_id);
CREATE INDEX shared_courses_public ON public.shared_courses(published, updated_at);
CREATE TABLE public.course_game_participants (
    game_id UUID NOT NULL REFERENCES public.course_games(id),
    account_id TEXT NOT NULL REFERENCES public."AspNetUsers"("Id"),
    name TEXT NOT NULL,
    round_id VARCHAR(32) NOT NULL CHECK (round_id ~ '^[a-f0-9]{32}$'),
    results JSONB NULL CHECK (results IS NULL OR jsonb_typeof(results) = 'array'),
    total_sg DOUBLE PRECISION NULL,
    PRIMARY KEY (game_id, account_id),
    CHECK ((results IS NULL) = (total_sg IS NULL))
);
CREATE INDEX course_game_participants_account ON public.course_game_participants(account_id);
