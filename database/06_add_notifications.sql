-- =============================================================================
-- Migration: notification_types szótártábla + notifications tábla
-- =============================================================================

CREATE TABLE notification_types (
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

CREATE TRIGGER trg_notification_types_audit
    BEFORE UPDATE ON notification_types
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();

INSERT INTO notification_types (id, title, description, create_user_id, modify_user_id, modify_date) VALUES
    (1, 'NewVideo',       'Új videó egy feliratkozott csatornán', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (2, 'NewComment',     'Új hozzászólás a videódon',            'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (3, 'CommentReply',   'Válasz a hozzászólásodra',             'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now()),
    (4, 'VideoProcessed', 'Videó feldolgozás befejeződött',       'f47ac10b-58cc-4372-a567-0e02b2c3d479', 'f47ac10b-58cc-4372-a567-0e02b2c3d479', now());

-- -----------------------------------------------------------------------------

CREATE TABLE notifications (
    id             uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id        uuid         NOT NULL REFERENCES users(id),
    type_id        integer      NOT NULL REFERENCES notification_types(id),
    title          text         NOT NULL,
    body           text,
    link           text,
    is_read        boolean      NOT NULL DEFAULT false,

    create_user_id uuid,
    create_date    timestamptz  NOT NULL DEFAULT now(),
    modify_user_id uuid,
    modify_date    timestamptz,
    active         boolean      NOT NULL DEFAULT true,
    version        int          NOT NULL DEFAULT 1
);

CREATE INDEX idx_notifications_user_id     ON notifications(user_id);
CREATE INDEX idx_notifications_user_unread ON notifications(user_id, is_read) WHERE active = true;

CREATE TRIGGER trg_notifications_audit
    BEFORE UPDATE ON notifications
    FOR EACH ROW EXECUTE FUNCTION set_audit_fields();
