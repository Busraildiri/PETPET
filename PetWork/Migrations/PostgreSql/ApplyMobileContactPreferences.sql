BEGIN;

CREATE TABLE IF NOT EXISTS petwork."MobileContactPreferences" (
    "UserId" integer PRIMARY KEY,
    "Phone" character varying(30),
    "ContactEmail" character varying(254),
    "AllowPatiMatchSharing" boolean NOT NULL DEFAULT FALSE,
    "AllowAdoptionSharing" boolean NOT NULL DEFAULT FALSE,
    "AllowLostPetSharing" boolean NOT NULL DEFAULT FALSE,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "FK_MobileContactPreferences_Users_UserId"
        FOREIGN KEY ("UserId") REFERENCES petwork."Users" ("Id") ON DELETE CASCADE
);

-- Kimlik doğrulama ve kullanıcı yetkilendirmesi ASP.NET API katmanında yapılır.
-- Backend petwork_app rolüyle çalıştığı için politikasız Supabase RLS tüm yazmaları engeller.
ALTER TABLE petwork."MobileContactPreferences" DISABLE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE petwork."MobileContactPreferences" TO petwork_app;

INSERT INTO petwork."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260912123109_AddMobileContactPreferencesPostgres', '9.0.4'
WHERE NOT EXISTS (
    SELECT 1 FROM petwork."__EFMigrationsHistory"
    WHERE "MigrationId" = '20260912123109_AddMobileContactPreferencesPostgres'
);

COMMIT;
