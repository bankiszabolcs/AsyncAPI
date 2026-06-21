-- =============================================================================
-- Migration: saved_videos tábla
-- =============================================================================

CREATE TABLE saved_videos (
    user_id        uuid        NOT NULL REFERENCES users(id),
    video_id       uuid        NOT NULL REFERENCES videos(id),

    create_user_id uuid,
    create_date    timestamptz NOT NULL DEFAULT now(),
    modify_user_id uuid,
    modify_date    timestamptz,
    active         boolean     NOT NULL DEFAULT true,
    version        int         NOT NULL DEFAULT 1,

    CONSTRAINT saved_videos_pkey PRIMARY KEY (user_id, video_id)
);

CREATE INDEX idx_saved_videos_video_id ON saved_videos(video_id);

CREATE TRIGGER trg_saved_videos_audit
    BEFORE UPDATE ON saved_videos
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();
