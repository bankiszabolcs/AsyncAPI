-- =============================================================================
-- Migration: categories tábla + category_id a videos táblán
-- =============================================================================

CREATE TABLE categories (
    id             uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    title          varchar(100) NOT NULL,
    description    text,

    create_user_id uuid,
    create_date    timestamptz NOT NULL DEFAULT now(),
    modify_user_id uuid,
    modify_date    timestamptz,
    active         boolean NOT NULL DEFAULT true,
    version        int     NOT NULL DEFAULT 1
);

CREATE TRIGGER trg_categories_audit
    BEFORE UPDATE ON categories
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

ALTER TABLE videos
    ADD COLUMN category_id uuid REFERENCES categories(id);

CREATE INDEX idx_videos_category_id ON videos(category_id);

INSERT INTO categories (title, description) VALUES
    ('Sport',               'Sportesemények, edzések, sportriportok'),
    ('Podcast',             'Beszélgetős műsorok, interjúk, hangos tartalmak'),
    ('Zene',                'Zenei klipek, koncertek, zenei oktatás'),
    ('Gaming',              'Videójátékok, livestreamek, gameplay videók'),
    ('Film & Sorozat',      'Filmkritikák, sorozatok, trailerek, recenziók'),
    ('Tech & Tudomány',     'Technológia, tudományos témák, gadgetek, AI'),
    ('Oktatás',             'Tananyagok, tutorialok, online kurzusok'),
    ('Humor & Szórakozás',  'Vígjátékok, sketchek, kihívások, prank videók'),
    ('Hírek & Politika',    'Aktuális hírek, politikai elemzések, riportok'),
    ('Életmód & Utazás',    'Utazási élmények, vlog, egészséges életmód'),
    ('Főzés & Gasztronómia','Receptek, éttermi értékelések, food vlog'),
    ('Autók & Járművek',    'Autótesztek, motorsport, járműves tartalmak'),
    ('Állatok & Természet', 'Állatok, természetfilmek, környezetvédelem'),
    ('Divat & Szépség',     'Stílus, smink, divattrendek, beauty tippek'),
    ('Fitness & Egészség',  'Edzéstervek, egészséges táplálkozás, wellness'),
    ('Egyéb',               'Minden egyéb, kategóriába nem sorolható tartalom');
