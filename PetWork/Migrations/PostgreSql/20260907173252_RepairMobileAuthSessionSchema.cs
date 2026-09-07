using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class RepairMobileAuthSessionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MobileAuthLifecycle was changed after it had already been recorded as
            // applied in production. Repair the resulting schema drift forward.
            migrationBuilder.Sql("""
                ALTER TABLE petwork."MobileAuthSessions"
                    ADD COLUMN IF NOT EXISTS "LastUsedAt" timestamp with time zone;
                ALTER TABLE petwork."MobileAuthSessions"
                    ADD COLUMN IF NOT EXISTS "AccessExpiresAt" timestamp with time zone;
                ALTER TABLE petwork."MobileAuthSessions"
                    ADD COLUMN IF NOT EXISTS "RefreshExpiresAt" timestamp with time zone;

                DO $repair$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'petwork'
                          AND table_name = 'MobileAuthSessions'
                          AND column_name = 'ExpiresAt'
                    ) THEN
                        EXECUTE $sql$
                            UPDATE petwork."MobileAuthSessions"
                               SET "LastUsedAt" = COALESCE("LastUsedAt", "CreatedAt"),
                                   "AccessExpiresAt" = COALESCE(
                                       "AccessExpiresAt",
                                       LEAST(COALESCE("ExpiresAt", "CreatedAt" + interval '15 minutes'), "CreatedAt" + interval '15 minutes')
                                   ),
                                   "RefreshExpiresAt" = COALESCE("RefreshExpiresAt", "ExpiresAt", "CreatedAt" + interval '1 day')
                        $sql$;
                    ELSE
                        UPDATE petwork."MobileAuthSessions"
                           SET "LastUsedAt" = COALESCE("LastUsedAt", "CreatedAt"),
                               "AccessExpiresAt" = COALESCE("AccessExpiresAt", "CreatedAt" + interval '15 minutes'),
                               "RefreshExpiresAt" = COALESCE("RefreshExpiresAt", "CreatedAt" + interval '1 day');
                    END IF;
                END
                $repair$;

                ALTER TABLE petwork."MobileAuthSessions"
                    ALTER COLUMN "LastUsedAt" SET NOT NULL;
                ALTER TABLE petwork."MobileAuthSessions"
                    ALTER COLUMN "AccessExpiresAt" SET NOT NULL;
                ALTER TABLE petwork."MobileAuthSessions"
                    ALTER COLUMN "RefreshExpiresAt" SET NOT NULL;

                ALTER TABLE petwork."MobileAuthSessions" DROP COLUMN IF EXISTS "ExpiresAt";
                ALTER TABLE petwork."MobileAuthSessions" DROP COLUMN IF EXISTS "ReplacedByTokenHash";

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MobileAuthSessions_RefreshTokenHash"
                    ON petwork."MobileAuthSessions" ("RefreshTokenHash");
                CREATE INDEX IF NOT EXISTS "IX_MobileAuthSessions_UserId_RevokedAt_RefreshExpiresAt"
                    ON petwork."MobileAuthSessions" ("UserId", "RevokedAt", "RefreshExpiresAt");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration repairs an already-applied production schema. Removing
            // the columns on rollback would destroy valid session data.
        }
    }
}
