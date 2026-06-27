-- =============================================================================
-- AsyncAPI — YouTube-szerű médiaplatform adatbázis séma (PostgreSQL 17)
-- Database-first: ez a script a séma forrása, az EF Core ebből scaffoldol.
-- Audit history: pgmemento kerül rá külön scriptben (02_pgmemento.sql).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Közös audit trigger: UPDATE-nél automatikusan frissíti a modify_date-et
-- és lépteti a version-t. A create_* oszlopokat az alkalmazás tölti ki.
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION set_audit_fields()
RETURNS trigger AS $$
BEGIN
    NEW.modify_date := now();
    NEW.version     := OLD.version + 1;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- -----------------------------------------------------------------------------
-- Szótártáblák (lookup tables)
-- Enum típusok helyett: rugalmasabb, bővíthető, scaffoldolható
-- Az integer ID értékek korrelálnak a C# enum értékeivel
-- -----------------------------------------------------------------------------

CREATE TABLE processing_statuses (
    id             INTEGER      PRIMARY KEY,
    title          VARCHAR(100) NOT NULL,
    description    TEXT,

    create_user_id uuid,
    create_date    timestamptz NOT NULL DEFAULT now(),
    modify_user_id uuid,
    modify_date    timestamptz,
    active         boolean NOT NULL DEFAULT true,
    version        int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_processing_statuses_audit
    BEFORE UPDATE ON processing_statuses
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

INSERT INTO processing_statuses (id, title, description, create_user_id, modify_user_id, modify_date) VALUES
    (1, 'Queued',     'Feldolgozásra várakozik',        'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (2, 'Processing', 'Feldolgozás folyamatban',         'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (3, 'Completed',  'Feldolgozás sikeresen befejezve', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (4, 'Failed',     'Feldolgozás sikertelen',          'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now());

-- -----------------------------------------------------------------------------

CREATE TABLE visibilities (
    id             INTEGER      PRIMARY KEY,
    title          VARCHAR(100) NOT NULL,
    description    TEXT,

    create_user_id uuid,
    create_date    timestamptz NOT NULL DEFAULT now(),
    modify_user_id uuid,
    modify_date    timestamptz,
    active         boolean NOT NULL DEFAULT true,
    version        int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_visibilities_audit
    BEFORE UPDATE ON visibilities
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

INSERT INTO visibilities (id, title, description, create_user_id, modify_user_id, modify_date) VALUES
    (1, 'Public',   'Mindenki láthatja',           'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (2, 'Unlisted', 'Csak link alapján érhető el', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (3, 'Private',  'Csak a feltöltő láthatja',    'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now());

-- -----------------------------------------------------------------------------

CREATE TABLE reaction_types (
    id             INTEGER      PRIMARY KEY,
    title          VARCHAR(100) NOT NULL,
    description    TEXT,

    create_user_id uuid,
    create_date    timestamptz NOT NULL DEFAULT now(),
    modify_user_id uuid,
    modify_date    timestamptz,
    active         boolean NOT NULL DEFAULT true,
    version        int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_reaction_types_audit
    BEFORE UPDATE ON reaction_types
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

INSERT INTO reaction_types (id, title, description, create_user_id, modify_user_id, modify_date) VALUES
    (1, 'Like',    'Pozitív visszajelzés',  'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (2, 'Dislike', 'Negatív visszajelzés', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now());

-- -----------------------------------------------------------------------------
-- users — felhasználók (egyben "csatornák" is, mint a YouTube-on)
-- -----------------------------------------------------------------------------

CREATE TABLE users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    username        varchar(50)  NOT NULL UNIQUE,
    email           varchar(255) NOT NULL UNIQUE,
    display_name    varchar(100),
    avatar_image_id uuid,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_users_audit
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

-- -----------------------------------------------------------------------------
-- images — feltöltött képek (thumbnail pipeline)
-- -----------------------------------------------------------------------------

CREATE TABLE images (
    id                 uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            uuid    REFERENCES users(id),
    original_file_name varchar(255) NOT NULL,
    extension          varchar(10)  NOT NULL,
    status_id          integer NOT NULL DEFAULT 1 REFERENCES processing_statuses(id),
    width              int,
    height             int,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_images_audit
    BEFORE UPDATE ON images
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_images_user_id ON images(user_id);

ALTER TABLE users
    ADD CONSTRAINT fk_users_avatar_image
    FOREIGN KEY (avatar_image_id) REFERENCES images(id);

-- -----------------------------------------------------------------------------
-- videos — a platform központi táblája
-- -----------------------------------------------------------------------------

CREATE TABLE videos (
    id                 uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            uuid    NOT NULL REFERENCES users(id),
    title              varchar(200) NOT NULL,
    description        text,
    original_file_name varchar(255) NOT NULL,
    duration_seconds   int,
    status_id          integer NOT NULL DEFAULT 1 REFERENCES processing_statuses(id),
    visibility_id      integer NOT NULL DEFAULT 3 REFERENCES visibilities(id),
    published_at       timestamptz,
    thumbnail_image_id uuid    REFERENCES images(id),

    view_count    bigint NOT NULL DEFAULT 0,
    like_count    int    NOT NULL DEFAULT 0,
    dislike_count int    NOT NULL DEFAULT 0,
    comment_count int    NOT NULL DEFAULT 0,

    search_vector tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('simple', coalesce(title, '')), 'A') ||
        setweight(to_tsvector('simple', coalesce(description, '')), 'B')
    ) STORED,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_videos_audit
    BEFORE UPDATE ON videos
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_videos_user_id    ON videos(user_id);
CREATE INDEX idx_videos_status_id  ON videos(status_id);
CREATE INDEX idx_videos_listing    ON videos(visibility_id, published_at DESC) WHERE active = true;
CREATE INDEX idx_videos_search     ON videos USING gin(search_vector);

-- -----------------------------------------------------------------------------
-- video_views
-- -----------------------------------------------------------------------------

CREATE TABLE video_views (
    id              bigserial   PRIMARY KEY,
    video_id        uuid        NOT NULL REFERENCES videos(id),
    user_id         uuid        REFERENCES users(id),
    watched_at      timestamptz NOT NULL DEFAULT now(),
    watched_seconds int,
    ip_address      inet,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_video_views_audit
    BEFORE UPDATE ON video_views
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_video_views_video_id   ON video_views(video_id);
CREATE INDEX idx_video_views_watched_at ON video_views(watched_at);

-- -----------------------------------------------------------------------------
-- tags + video_tags
-- -----------------------------------------------------------------------------

CREATE TABLE tags (
    id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(50) NOT NULL UNIQUE,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_tags_audit
    BEFORE UPDATE ON tags
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE TABLE video_tags (
    video_id uuid NOT NULL REFERENCES videos(id) ON DELETE CASCADE,
    tag_id   uuid NOT NULL REFERENCES tags(id)   ON DELETE CASCADE,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1,

    PRIMARY KEY (video_id, tag_id)
);

CREATE TRIGGER trg_video_tags_audit
    BEFORE UPDATE ON video_tags
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_video_tags_tag_id ON video_tags(tag_id);

-- -----------------------------------------------------------------------------
-- comments
-- -----------------------------------------------------------------------------

CREATE TABLE comments (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    video_id          uuid NOT NULL REFERENCES videos(id) ON DELETE CASCADE,
    user_id           uuid NOT NULL REFERENCES users(id),
    parent_comment_id uuid REFERENCES comments(id),
    content           text NOT NULL,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_comments_audit
    BEFORE UPDATE ON comments
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_comments_video_id  ON comments(video_id);
CREATE INDEX idx_comments_parent_id ON comments(parent_comment_id);

-- -----------------------------------------------------------------------------
-- video_reactions
-- -----------------------------------------------------------------------------

CREATE TABLE video_reactions (
    video_id         uuid    NOT NULL REFERENCES videos(id) ON DELETE CASCADE,
    user_id          uuid    NOT NULL REFERENCES users(id),
    reaction_type_id integer NOT NULL REFERENCES reaction_types(id),

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1,

    PRIMARY KEY (video_id, user_id)
);

CREATE TRIGGER trg_video_reactions_audit
    BEFORE UPDATE ON video_reactions
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

-- -----------------------------------------------------------------------------
-- subscriptions
-- -----------------------------------------------------------------------------

CREATE TABLE subscriptions (
    subscriber_id uuid NOT NULL REFERENCES users(id),
    channel_id    uuid NOT NULL REFERENCES users(id),

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1,

    PRIMARY KEY (subscriber_id, channel_id),
    CHECK (subscriber_id <> channel_id)
);

CREATE TRIGGER trg_subscriptions_audit
    BEFORE UPDATE ON subscriptions
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_subscriptions_channel_id ON subscriptions(channel_id);

-- -----------------------------------------------------------------------------
-- playlists + playlist_videos
-- -----------------------------------------------------------------------------

CREATE TABLE playlists (
    id            uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       uuid    NOT NULL REFERENCES users(id),
    title         varchar(200) NOT NULL,
    description   text,
    visibility_id integer NOT NULL DEFAULT 3 REFERENCES visibilities(id),

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_playlists_audit
    BEFORE UPDATE ON playlists
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_playlists_user_id ON playlists(user_id);

CREATE TABLE playlist_videos (
    playlist_id uuid NOT NULL REFERENCES playlists(id) ON DELETE CASCADE,
    video_id    uuid NOT NULL REFERENCES videos(id)    ON DELETE CASCADE,
    position    int  NOT NULL,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1,

    PRIMARY KEY (playlist_id, video_id)
);

CREATE TRIGGER trg_playlist_videos_audit
    BEFORE UPDATE ON playlist_videos
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_playlist_videos_video_id ON playlist_videos(video_id);

-- -----------------------------------------------------------------------------
-- Technical user — feltöltések tulajdonosa auth bevezetéséig
-- -----------------------------------------------------------------------------

INSERT INTO users (id, username, email, display_name, create_user_id, modify_user_id, modify_date) VALUES
    ('f47ac10b-58cc-4372-a567-0e02b2c3d479', 'system', 'system@asyncapi.internal', 'System', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now());
