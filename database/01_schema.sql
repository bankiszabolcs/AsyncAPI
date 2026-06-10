-- =============================================================================
-- AsyncAPI — YouTube-szerű médiaplatform adatbázis séma (PostgreSQL 17)
-- Database-first: ez a script a séma forrása, az EF Core ebből scaffoldol.
-- Audit history: pgmemento kerül rá külön scriptben (02_pgmemento.sql).
-- =============================================================================

-- gen_random_uuid()-hoz PostgreSQL 13+ alatt beépített, extension nem kell

-- -----------------------------------------------------------------------------
-- ENUM típusok
-- -----------------------------------------------------------------------------

-- A feldolgozási státusz ugyanaz a négy állapot, amit a Redis-ben is használunk;
-- a DB a perzisztens "végső igazság", a Redis a gyors, valós idejű réteg
CREATE TYPE processing_status AS ENUM ('queued', 'processing', 'completed', 'failed');

-- Videó láthatósága — YouTube minta: publikus / link birtokában / privát
CREATE TYPE visibility AS ENUM ('public', 'unlisted', 'private');

-- Reakció típusa (like/dislike egy táblában, hogy egy user csak egyet adhasson)
CREATE TYPE reaction_type AS ENUM ('like', 'dislike');

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
-- users — felhasználók (egyben "csatornák" is, mint a YouTube-on)
-- -----------------------------------------------------------------------------

CREATE TABLE users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    username        varchar(50)  NOT NULL UNIQUE,
    email           varchar(255) NOT NULL UNIQUE,
    display_name    varchar(100),                 -- megjelenített név (csatornanév)
    avatar_image_id uuid,                         -- profilkép; FK később (images még nem létezik itt)

    -- audit oszlopok
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
-- A MinIO-ban az images bucket {id}/ prefixe alatt vannak a méretezett változatok
-- -----------------------------------------------------------------------------

CREATE TABLE images (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            uuid REFERENCES users (id),
    original_file_name varchar(255) NOT NULL,     -- ahogy a feltöltő gépén hívták
    extension          varchar(10)  NOT NULL,     -- pl. ".jpg" — a MinIO URL-hez kell
    status             processing_status NOT NULL DEFAULT 'queued',
    width              int,                       -- eredeti kép szélessége pixelben
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

CREATE INDEX idx_images_user_id ON images (user_id);

-- users.avatar_image_id FK utólag, mert az images tábla a users után jött létre
ALTER TABLE users
    ADD CONSTRAINT fk_users_avatar_image
    FOREIGN KEY (avatar_image_id) REFERENCES images (id);

-- -----------------------------------------------------------------------------
-- videos — a platform központi táblája
-- A MinIO-ban a videos bucket {id}/ prefixe alatt: master.m3u8, minőségek, sprite
-- -----------------------------------------------------------------------------

CREATE TABLE videos (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id            uuid NOT NULL REFERENCES users (id),  -- a feltöltő (csatorna)
    title              varchar(200) NOT NULL,
    description        text,
    original_file_name varchar(255) NOT NULL,
    duration_seconds   int,                       -- FFProbe-ból, feldolgozás után töltjük
    status             processing_status NOT NULL DEFAULT 'queued',
    visibility         visibility NOT NULL DEFAULT 'private',
    published_at       timestamptz,               -- mikor lett publikusra állítva
    thumbnail_image_id uuid REFERENCES images (id),  -- borítókép (custom thumbnail)

    -- Denormalizált számlálók: a Redis-ből szinkronizáljuk őket időnként.
    -- Listázásnál így nem kell a milliós video_views táblát aggregálni.
    view_count         bigint NOT NULL DEFAULT 0,
    like_count         int    NOT NULL DEFAULT 0,
    dislike_count      int    NOT NULL DEFAULT 0,
    comment_count      int    NOT NULL DEFAULT 0,

    -- Full-text kereséshez generált oszlop: title duplán súlyozva (A), description (B)
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

CREATE INDEX idx_videos_user_id     ON videos (user_id);
CREATE INDEX idx_videos_status      ON videos (status);
-- Főoldali listázás tipikus lekérdezése: publikus, aktív, legújabb elöl
CREATE INDEX idx_videos_listing     ON videos (visibility, published_at DESC) WHERE active = true;
-- Full-text search index
CREATE INDEX idx_videos_search      ON videos USING gin (search_vector);

-- -----------------------------------------------------------------------------
-- video_views — minden megtekintés egy sor (analytics alap)
-- Nagy forgalomnál ez milliós tábla lesz; bigserial PK olcsóbb mint az uuid
-- -----------------------------------------------------------------------------

CREATE TABLE video_views (
    id              bigserial PRIMARY KEY,
    video_id        uuid NOT NULL REFERENCES videos (id),
    user_id         uuid REFERENCES users (id),   -- NULL = nem bejelentkezett néző
    watched_at      timestamptz NOT NULL DEFAULT now(),
    watched_seconds int,                          -- retenció méréshez (meddig nézte)
    ip_address      inet,                         -- vendég nézők hozzávetőleges azonosítása

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

CREATE INDEX idx_video_views_video_id   ON video_views (video_id);
CREATE INDEX idx_video_views_watched_at ON video_views (watched_at);

-- -----------------------------------------------------------------------------
-- tags + video_tags — N:M címkézés
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
    video_id uuid NOT NULL REFERENCES videos (id) ON DELETE CASCADE,
    tag_id   uuid NOT NULL REFERENCES tags (id)   ON DELETE CASCADE,

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

CREATE INDEX idx_video_tags_tag_id ON video_tags (tag_id);

-- -----------------------------------------------------------------------------
-- comments — kommentek, válaszokkal (önhivatkozó FK)
-- -----------------------------------------------------------------------------

CREATE TABLE comments (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    video_id          uuid NOT NULL REFERENCES videos (id) ON DELETE CASCADE,
    user_id           uuid NOT NULL REFERENCES users (id),
    parent_comment_id uuid REFERENCES comments (id),  -- NULL = gyökér komment, kitöltve = válasz
    content           text NOT NULL,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,  -- moderálás: false = törölt/rejtett
    version         int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_comments_audit
    BEFORE UPDATE ON comments
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_comments_video_id  ON comments (video_id);
CREATE INDEX idx_comments_parent_id ON comments (parent_comment_id);

-- -----------------------------------------------------------------------------
-- video_reactions — like/dislike; egy user egy videóra csak egyet adhat
-- -----------------------------------------------------------------------------

CREATE TABLE video_reactions (
    video_id uuid NOT NULL REFERENCES videos (id) ON DELETE CASCADE,
    user_id  uuid NOT NULL REFERENCES users (id),
    reaction reaction_type NOT NULL,

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,
    version         int     NOT NULL DEFAULT 1,

    PRIMARY KEY (video_id, user_id)  -- ez garantálja az "egy user egy reakció" szabályt
);

CREATE TRIGGER trg_video_reactions_audit
    BEFORE UPDATE ON video_reactions
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

-- -----------------------------------------------------------------------------
-- subscriptions — feliratkozások (user követ egy csatornát, azaz egy másik usert)
-- -----------------------------------------------------------------------------

CREATE TABLE subscriptions (
    subscriber_id uuid NOT NULL REFERENCES users (id),  -- aki feliratkozik
    channel_id    uuid NOT NULL REFERENCES users (id),  -- akire feliratkozik

    create_user_id  uuid,
    create_date     timestamptz NOT NULL DEFAULT now(),
    modify_user_id  uuid,
    modify_date     timestamptz,
    active          boolean NOT NULL DEFAULT true,  -- leiratkozás = active false (history megmarad)
    version         int     NOT NULL DEFAULT 1,

    PRIMARY KEY (subscriber_id, channel_id),
    CHECK (subscriber_id <> channel_id)  -- saját magára nem iratkozhat fel
);

CREATE TRIGGER trg_subscriptions_audit
    BEFORE UPDATE ON subscriptions
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

CREATE INDEX idx_subscriptions_channel_id ON subscriptions (channel_id);

-- -----------------------------------------------------------------------------
-- playlists + playlist_videos — lejátszási listák
-- -----------------------------------------------------------------------------

CREATE TABLE playlists (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users (id),
    title       varchar(200) NOT NULL,
    description text,
    visibility  visibility NOT NULL DEFAULT 'private',

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

CREATE INDEX idx_playlists_user_id ON playlists (user_id);

CREATE TABLE playlist_videos (
    playlist_id uuid NOT NULL REFERENCES playlists (id) ON DELETE CASCADE,
    video_id    uuid NOT NULL REFERENCES videos (id)    ON DELETE CASCADE,
    position    int  NOT NULL,  -- sorrend a listán belül

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

CREATE INDEX idx_playlist_videos_video_id ON playlist_videos (video_id);
