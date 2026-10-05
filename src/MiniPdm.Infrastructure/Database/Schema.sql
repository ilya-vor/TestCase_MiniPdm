CREATE TABLE IF NOT EXISTS pdm_object (
    id                 uuid PRIMARY KEY,
    object_type        text NOT NULL,
    designation        text NULL,
    name               text NOT NULL,
    current_version_id uuid NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_pdm_object_designation
    ON pdm_object (designation) WHERE designation IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_pdm_object_std_name
    ON pdm_object (name) WHERE object_type = 'StandardPart';

CREATE TABLE IF NOT EXISTS object_version (
    id          uuid PRIMARY KEY,
    object_id   uuid NOT NULL REFERENCES pdm_object (id) ON DELETE CASCADE,
    version_no  integer NOT NULL CHECK (version_no > 0),
    state       text NOT NULL,
    material    text NULL,
    mass_kg     numeric(18, 6) NULL CHECK (mass_kg IS NULL OR mass_kg >= 0),
    created_at  timestamptz NOT NULL,
    CONSTRAINT uq_object_version_no UNIQUE (object_id, version_no)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_pdm_object_current_version') THEN
        ALTER TABLE pdm_object
            ADD CONSTRAINT fk_pdm_object_current_version
            FOREIGN KEY (current_version_id) REFERENCES object_version (id);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS bom_link (
    id                uuid PRIMARY KEY,
    parent_version_id uuid NOT NULL REFERENCES object_version (id) ON DELETE CASCADE,
    child_object_id   uuid NOT NULL REFERENCES pdm_object (id),
    quantity          integer NOT NULL CHECK (quantity > 0),
    CONSTRAINT uq_bom_parent_child UNIQUE (parent_version_id, child_object_id)
);

CREATE TABLE IF NOT EXISTS import_log (
    id         bigserial PRIMARY KEY,
    started_at timestamptz NOT NULL,
    file_name  text NULL,
    severity   text NOT NULL,
    reason     text NULL
);

CREATE INDEX IF NOT EXISTS ix_bom_link_parent ON bom_link (parent_version_id);
CREATE INDEX IF NOT EXISTS ix_bom_link_child ON bom_link (child_object_id);
CREATE INDEX IF NOT EXISTS ix_object_version_object ON object_version (object_id);
