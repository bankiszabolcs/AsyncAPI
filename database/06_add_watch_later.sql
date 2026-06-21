-- =============================================================================
-- Migration: watch_later tábla
-- =============================================================================

CREATE TABLE watch_later (
    user_id        uuid        NOT NULL REFERENCES users(id),
    video_id       uuid        NOT NULL REFERENCES videos(id),

    create_user_id uuid,
    create_date    timestamptz NOT NULL DEFAULT now(),
    modify_user_id uuid,
    modify_date    timestamptz,
    active         boolean     NOT NULL DEFAULT true,
    version        int         NOT NULL DEFAULT 1,

    CONSTRAINT watch_later_pkey PRIMARY KEY (user_id, video_id)
);

CREATE INDEX idx_watch_later_video_id ON watch_later(video_id);

CREATE TRIGGER trg_watch_later_audit
    BEFORE UPDATE ON watch_later
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();
