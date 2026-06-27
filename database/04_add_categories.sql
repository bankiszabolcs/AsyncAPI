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

INSERT INTO categories (title, description, create_user_id, modify_user_id, modify_date) VALUES
    ('Sport',               'Sportesemények, edzések, sportriportok',           'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Podcast',             'Beszélgetős műsorok, interjúk, hangos tartalmak',  'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Zene',                'Zenei klipek, koncertek, zenei oktatás',           'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Gaming',              'Videójátékok, livestreamek, gameplay videók',       'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Film & Sorozat',      'Filmkritikák, sorozatok, trailerek, recenziók',    'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Tech & Tudomány',     'Technológia, tudományos témák, gadgetek, AI',      'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Oktatás',             'Tananyagok, tutorialok, online kurzusok',          'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Humor & Szórakozás',  'Vígjátékok, sketchek, kihívások, prank videók',   'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Hírek & Politika',    'Aktuális hírek, politikai elemzések, riportok',    'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Életmód & Utazás',    'Utazási élmények, vlog, egészséges életmód',       'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Főzés & Gasztronómia','Receptek, éttermi értékelések, food vlog',         'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Autók & Járművek',    'Autótesztek, motorsport, járműves tartalmak',      'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Állatok & Természet', 'Állatok, természetfilmek, környezetvédelem',       'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Divat & Szépség',     'Stílus, smink, divattrendek, beauty tippek',       'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Fitness & Egészség',  'Edzéstervek, egészséges táplálkozás, wellness',    'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    ('Egyéb',               'Minden egyéb, kategóriába nem sorolható tartalom', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now());
