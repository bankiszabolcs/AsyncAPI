-- Feltöltött videó eredeti mérete bájtban — kvóta számításhoz
ALTER TABLE videos ADD COLUMN IF NOT EXISTS file_size_bytes BIGINT NOT NULL DEFAULT 0;
