using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetWork.Migrations.PostgreSql;

public partial class CompleteMobileAuthProduction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing accounts predate e-mail verification and must remain usable.
        migrationBuilder.Sql("""
            DO $$
            DECLARE column_name text;
            BEGIN
                FOREACH column_name IN ARRAY ARRAY['CreatedAt', 'LastUsedAt', 'AccessExpiresAt', 'RefreshExpiresAt', 'RevokedAt']
                LOOP
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'petwork' AND table_name = 'MobileAuthSessions'
                          AND information_schema.columns.column_name = column_name
                          AND data_type = 'timestamp without time zone'
                    ) THEN
                        EXECUTE format(
                            'ALTER TABLE petwork."MobileAuthSessions" ALTER COLUMN %I TYPE timestamp with time zone USING %I AT TIME ZONE ''UTC''',
                            column_name, column_name);
                    END IF;
                END LOOP;

                FOREACH column_name IN ARRAY ARRAY['CreatedAt', 'ExpiresAt', 'UsedAt']
                LOOP
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'petwork' AND table_name = 'PasswordResetTokens'
                          AND information_schema.columns.column_name = column_name
                          AND data_type = 'timestamp without time zone'
                    ) THEN
                        EXECUTE format(
                            'ALTER TABLE petwork."PasswordResetTokens" ALTER COLUMN %I TYPE timestamp with time zone USING %I AT TIME ZONE ''UTC''',
                            column_name, column_name);
                    END IF;
                END LOOP;
            END $$;

            ALTER TABLE petwork."PasswordResetTokens"
                ALTER COLUMN "Id" TYPE bigint;
            ALTER TABLE petwork."Users"
                ADD COLUMN IF NOT EXISTS "IsEmailVerified" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE petwork."Users"
                ALTER COLUMN "IsEmailVerified" SET DEFAULT FALSE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsEmailVerified", schema: "petwork", table: "Users");
    }
}
