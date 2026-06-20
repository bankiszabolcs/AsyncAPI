CREATE OR REPLACE FUNCTION get_related_videos(
    p_video_id  UUID,
    p_user_id   UUID,     -- NULL ha vendég
    p_limit     INT DEFAULT 10
)
RETURNS TABLE (video_id UUID, score FLOAT8)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH
    -- Az aktuális videó tagjei
	current_tags AS (
	    SELECT tag_id
	    FROM   video_tags vt          -- ← alias hozzáadva
	    WHERE  vt.video_id = p_video_id AND vt.active = true
	),
    -- User nézettsége alapján tag-affinitás (completion × recency decay)
    user_watch_affinity AS (
        SELECT
            vt.tag_id,
            SUM(
                LEAST(COALESCE(vv.watched_seconds::float / NULLIF(v.duration_seconds, 0), 0), 1.0)
                * EXP(-EXTRACT(EPOCH FROM (NOW() - vv.watched_at)) / 2592000.0) -- 30 napos felezési idő
            ) AS affinity
        FROM video_views vv
        JOIN videos      v  ON v.id  = vv.video_id AND v.active = true
        JOIN video_tags  vt ON vt.video_id = vv.video_id AND vt.active = true
        WHERE vv.user_id = p_user_id AND vv.active = true
        GROUP BY vt.tag_id
    ),
    -- User like-jain alapuló tag-affinitás
    user_like_affinity AS (
        SELECT
            vt.tag_id,
            COUNT(*)::float AS like_weight
        FROM video_reactions vr
        JOIN video_tags vt ON vt.video_id = vr.video_id AND vt.active = true
        WHERE vr.user_id = p_user_id
          AND vr.reaction_type_id = 1   -- Like
          AND vr.active = true
        GROUP BY vt.tag_id
    ),
    -- Normalizáláshoz max értékek
    normalizers AS (
        SELECT
            GREATEST(MAX(affinity),    1.0) AS max_watch,
            GREATEST(MAX(like_weight), 1.0) AS max_like,
            GREATEST(MAX(v.view_count)::float, 1.0) AS max_views
        FROM user_watch_affinity, user_like_affinity,
             (SELECT view_count FROM videos WHERE active = true) v
    ),
    -- Tag metaadat összesítés jelöltenként (1 sor / videó, duplikáció nélkül)
    candidate_tags AS (
        SELECT
            vt.video_id,
            COUNT(DISTINCT vt.tag_id)::float                                     AS total_tags,
            COUNT(DISTINCT ct.tag_id)::float                                     AS shared_tags,
            COALESCE(SUM(DISTINCT uta.affinity),     0.0)                        AS watch_sum,
            COALESCE(SUM(DISTINCT ula.like_weight),  0.0)                        AS like_sum
        FROM video_tags vt
        LEFT JOIN current_tags      ct  ON ct.tag_id  = vt.tag_id
        LEFT JOIN user_watch_affinity uta ON uta.tag_id = vt.tag_id
        LEFT JOIN user_like_affinity  ula ON ula.tag_id = vt.tag_id
        WHERE vt.active = true
          AND vt.video_id != p_video_id
        GROUP BY vt.video_id
    )
    SELECT
        v.id AS video_id,
        -- Tag overlap: Jaccard-normált (megosztott / sqrt(jelölt_tag * aktuális_tag))
        0.45 * (ct.shared_tags / NULLIF(SQRT((SELECT COUNT(*)::float FROM current_tags) * ct.total_tags), 0))
        -- Watch history affinitás (0–1 normált)
      + 0.30 * (ct.watch_sum  / n.max_watch)
        -- Like affinitás (0–1 normált)
      + 0.15 * (ct.like_sum   / n.max_like)
        -- Popularitás tiebreaker (log-normált)
      + 0.10 * (LOG(v.view_count + 2) / LOG(n.max_views + 2))
        AS score
    FROM videos         v
    JOIN candidate_tags ct ON ct.video_id = v.id
    CROSS JOIN normalizers  n
    WHERE v.active        = true
      AND v.status_id     = 3   -- Completed
      AND v.visibility_id = 1   -- Public
      AND ct.shared_tags  > 0   -- legalább 1 közös tag kell
    ORDER BY score DESC
    LIMIT p_limit;
END;
$$;
