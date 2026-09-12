BEGIN;

ALTER TABLE petwork."AdoptionListings"
    ADD COLUMN IF NOT EXISTS "AllowInAppMessages" boolean NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "ContactPhone" character varying(32),
    ADD COLUMN IF NOT EXISTS "ContactEmail" character varying(254);

ALTER TABLE petwork."LostPetListings"
    ADD COLUMN IF NOT EXISTS "AllowInAppMessages" boolean NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "ContactPhone" character varying(32),
    ADD COLUMN IF NOT EXISTS "ContactEmail" character varying(254);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE petwork."AdoptionListings" TO petwork_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE petwork."LostPetListings" TO petwork_app;

INSERT INTO petwork."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260912132227_AddListingContactMethodsPostgres', '9.0.4'
WHERE NOT EXISTS (
    SELECT 1 FROM petwork."__EFMigrationsHistory"
    WHERE "MigrationId" = '20260912132227_AddListingContactMethodsPostgres'
);

COMMIT;
