using System.Buffers.Binary;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

return await MigrationProgram.RunAsync(args);

internal static class MigrationProgram
{
    private const string TargetSchema = "petwork";
    private const string AppRole = "petwork_app";
    private const string UserSecretsId = "PetWork-7f1c92d1-5e29-4b4d-a39d-782ae514cbed";

    private static readonly IReadOnlyList<TableSpec> Tables =
    [
        new("Users", true,
        [
            Text("Username"), Text("Email"), Text("PasswordHash"), Text("Bio"),
            Text("ProfileImage"), Timestamp("RegistrationDate"), Integer("ExperiencePoints"), Boolean("IsAdmin")
        ]),
        new("Badges", true, [Text("Name"), Text("Description"), Text("IconUrl")]),
        new("Diseases", true,
        [
            Text("Name"), Text("Description"), Text("Symptoms"), Text("Treatments"), Text("Treatment"),
            Text("Prevention"), Text("PetType"), Text("AnimalType"), Text("FeaturedImage"), Text("Category"),
            Timestamp("PublishDate"), Text("SeverityLevel"), Integer("ViewCount")
        ]),
        new("BlogPosts", true,
        [
            Text("Title"), Text("Content"), Text("Category"), Text("FeaturedImage"), Text("ImageUrl"),
            Timestamp("PublishedDate"), Timestamp("PublishDate"), Integer("ViewCount"), Integer("UserId")
        ]),
        new("Guides", true,
        [
            Text("Title"), Text("Description"), Text("Content"), Text("AnimalType"), Text("Category"),
            Text("Level"), Integer("ViewCount"), Timestamp("PublishDate"), Integer("UserId")
        ]),
        new("Pets", true,
        [
            Text("Name"), Text("Type"), Text("Breed"), Date("DateOfBirth"), Text("Gender"),
            Text("ProfileImage"), Text("Description"), Integer("UserId"), Integer("Age"), Text("Image"), Text("PetType")
        ]),
        new("Questions", true,
        [
            Text("Title"), Text("Content"), Text("Category"), Text("Tags"), Timestamp("CreatedDate"),
            Integer("ViewCount"), Integer("UserId")
        ]),
        new("Recipes", true,
        [
            Text("Title"), Text("Ingredients"), Text("Instructions"), Text("Content"), Text("PetType"),
            Text("AnimalType"), Text("FeaturedImage"), Text("ImageUrl"), Text("Difficulty"), Text("PrepTime"),
            Timestamp("CreatedDate"), Timestamp("PublishDate"), Integer("ViewCount"), Integer("UserId"),
            Text("Description"), Text("DietType"), Integer("PreparationTime")
        ]),
        new("Answers", true,
        [
            Text("Content"), Timestamp("CreatedDate"), Boolean("IsAccepted"), Integer("UpVotes"),
            Integer("DownVotes"), Integer("QuestionId"), Integer("UserId")
        ]),
        new("BadgeUser", false, [Integer("BadgesId"), Integer("UsersId")], ["BadgesId", "UsersId"])
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        var validCommand = args.Length == 1 &&
            args[0] is "preflight" or "migrate" or "verify" or "diagnose" or "provision-app-role";
        var validGrantCommand = args.Length == 2 && args[0] == "grant-site-admin";
        if (!validCommand && !validGrantCommand)
        {
            Console.Error.WriteLine(
                "Usage: dotnet run -- <preflight|migrate|verify|diagnose|provision-app-role> OR grant-site-admin <username>");
            return 2;
        }

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<SecretMarker>(optional: true)
            .Build();

        var sourceConnectionString = configuration.GetConnectionString("SqlServerSource");
        var targetConnectionString = configuration.GetConnectionString("PostgreSqlAdmin");

        if (string.IsNullOrWhiteSpace(sourceConnectionString) || string.IsNullOrWhiteSpace(targetConnectionString))
        {
            Console.Error.WriteLine(
                "Missing User Secrets: ConnectionStrings:SqlServerSource and/or ConnectionStrings:PostgreSqlAdmin.");
            return 3;
        }

        try
        {
            await using var source = new SqlConnection(sourceConnectionString);
            await using var target = new NpgsqlConnection(NormalizePostgreSqlConnectionString(targetConnectionString));
            await source.OpenAsync();
            await target.OpenAsync();

            return args[0] switch
            {
                "preflight" => await PreflightAsync(source, target),
                "migrate" => await MigrateAsync(source, target),
                "verify" => await VerifyAsync(source, target),
                "diagnose" => await DiagnoseAsync(source, target),
                "provision-app-role" => await ProvisionAppRoleAsync(target),
                "grant-site-admin" => await GrantSiteAdminAsync(target, args[1]),
                _ => 2
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Migration command failed: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    private static string NormalizePostgreSqlConnectionString(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 2)
        {
            throw new FormatException("PostgreSQL URI must include a username and password.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.Require
        }.ConnectionString;
    }

    private static async Task<int> PreflightAsync(SqlConnection source, NpgsqlConnection target)
    {
        await using (var sourceCommand = source.CreateCommand())
        {
            sourceCommand.CommandText = "SELECT DB_NAME();";
            var sourceDatabase = Convert.ToString(await sourceCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Console.WriteLine($"Source database: {sourceDatabase}");
        }

        await using (var targetCommand = target.CreateCommand())
        {
            targetCommand.CommandText = "SELECT current_database();";
            var targetDatabase = Convert.ToString(await targetCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Console.WriteLine($"Target database: {targetDatabase}");
        }

        var schemaExists = await TargetSchemaExistsAsync(target);
        Console.WriteLine($"Target schema '{TargetSchema}' exists: {schemaExists}");

        foreach (var table in Tables)
        {
            var sourceCount = await CountSourceAsync(source, table);
            var targetCount = schemaExists ? await CountTargetIfPresentAsync(target, table) : null;
            Console.WriteLine($"{table.Name}: source={sourceCount}, target={(targetCount?.ToString() ?? "not-created")}");
        }

        return 0;
    }

    private static async Task<int> MigrateAsync(SqlConnection source, NpgsqlConnection target)
    {
        if (!await TargetSchemaExistsAsync(target))
        {
            throw new InvalidOperationException(
                "Target schema is missing. Apply the PostgreSQL EF migration before copying data.");
        }

        foreach (var table in Tables)
        {
            var targetCount = await CountTargetIfPresentAsync(target, table)
                ?? throw new InvalidOperationException($"Target table {TargetSchema}.{table.Name} is missing.");

            if (targetCount != 0)
            {
                throw new InvalidOperationException(
                    $"Target table {TargetSchema}.{table.Name} contains {targetCount} rows. Refusing to delete or overwrite data.");
            }
        }

        await using var transaction = await target.BeginTransactionAsync();
        try
        {
            foreach (var table in Tables)
            {
                var copied = await CopyTableAsync(source, target, transaction, table);
                var sourceCount = await CountSourceAsync(source, table);
                var targetCount = await CountTargetAsync(target, transaction, table);

                if (copied != sourceCount || targetCount != sourceCount)
                {
                    throw new InvalidOperationException(
                        $"Count mismatch for {table.Name}: source={sourceCount}, copied={copied}, target={targetCount}.");
                }

                Console.WriteLine($"Copied {table.Name}: {copied} rows");
            }

            foreach (var table in Tables.Where(table => table.HasIdentity))
            {
                await ResetIdentityAsync(target, transaction, table);
            }

            await transaction.CommitAsync();
            Console.WriteLine("Migration committed successfully.");
            return 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            Console.Error.WriteLine("Migration rolled back; no partial copied rows were committed.");
            throw;
        }
    }

    private static async Task<int> VerifyAsync(SqlConnection source, NpgsqlConnection target)
    {
        if (!await TargetSchemaExistsAsync(target))
        {
            throw new InvalidOperationException("Target schema is missing.");
        }

        var allEqual = true;
        foreach (var table in Tables)
        {
            var sourceResult = await DigestSourceAsync(source, table);
            var targetResult = await DigestTargetAsync(target, table);
            var equal = sourceResult == targetResult;
            allEqual &= equal;

            Console.WriteLine(
                $"{table.Name}: source={sourceResult.Count}, target={targetResult.Count}, digestMatch={equal}");
        }

        var orphanCount = await CountTargetOrphansAsync(target);
        Console.WriteLine($"Foreign-key orphan rows: {orphanCount}");
        allEqual &= orphanCount == 0;

        Console.WriteLine(allEqual ? "Verification passed." : "Verification failed.");
        return allEqual ? 0 : 4;
    }

    private static async Task<int> DiagnoseAsync(SqlConnection source, NpgsqlConnection target)
    {
        var mismatchCount = 0;

        foreach (var table in Tables)
        {
            await using var sourceCommand = source.CreateCommand();
            sourceCommand.CommandText = BuildSourceSelect(table);
            await using var targetCommand = target.CreateCommand();
            targetCommand.CommandText = BuildTargetSelect(table);

            var sourceRows = await ReadNormalizedRowsAsync(sourceCommand, table);
            var targetRows = await ReadNormalizedRowsAsync(targetCommand, table);
            var rowCount = Math.Min(sourceRows.Count, targetRows.Count);

            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var differingColumns = new List<string>();
                for (var columnIndex = 0; columnIndex < table.AllColumns.Count; columnIndex++)
                {
                    if (!string.Equals(
                            sourceRows[rowIndex][columnIndex],
                            targetRows[rowIndex][columnIndex],
                            StringComparison.Ordinal))
                    {
                        differingColumns.Add(table.AllColumns[columnIndex].Name);
                    }
                }

                if (differingColumns.Count == 0)
                {
                    continue;
                }

                mismatchCount++;
                var key = string.Join(",", table.KeyColumns.Select(keyColumn =>
                {
                    var keyIndex = table.AllColumns
                        .Select((column, index) => (column, index))
                        .Single(item => item.column.Name == keyColumn)
                        .index;
                    return $"{keyColumn}={sourceRows[rowIndex][keyIndex]}";
                }));
                Console.WriteLine($"{table.Name} ({key}): {string.Join(", ", differingColumns)}");
            }

            if (sourceRows.Count != targetRows.Count)
            {
                mismatchCount++;
                Console.WriteLine($"{table.Name}: row-count mismatch during diagnostics.");
            }
        }

        Console.WriteLine($"Diagnostic mismatched rows: {mismatchCount}");
        return mismatchCount == 0 ? 0 : 5;
    }

    private static async Task<int> ProvisionAppRoleAsync(NpgsqlConnection target)
    {
        await using var existsCommand = target.CreateCommand();
        existsCommand.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @role);";
        existsCommand.Parameters.AddWithValue("role", AppRole);
        if (Convert.ToBoolean(await existsCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException(
                $"Role {AppRole} already exists. Refusing to rotate its password automatically.");
        }

        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var transaction = await target.BeginTransactionAsync();
        try
        {
            string roleSql;
            await using (var formatCommand = target.CreateCommand())
            {
                formatCommand.Transaction = transaction;
                formatCommand.CommandText = """
                    SELECT format(
                        'CREATE ROLE %1$I WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %2$L;
                         GRANT CONNECT ON DATABASE %3$I TO %1$I;
                         GRANT USAGE ON SCHEMA %4$I TO %1$I;
                         GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %4$I TO %1$I;
                         GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %4$I TO %1$I;
                         ALTER DEFAULT PRIVILEGES IN SCHEMA %4$I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %1$I;
                         ALTER DEFAULT PRIVILEGES IN SCHEMA %4$I GRANT USAGE, SELECT ON SEQUENCES TO %1$I;',
                        @role, @password, current_database(), @schema);
                    """;
                formatCommand.Parameters.AddWithValue("role", AppRole);
                formatCommand.Parameters.AddWithValue("password", password);
                formatCommand.Parameters.AddWithValue("schema", TargetSchema);
                roleSql = Convert.ToString(await formatCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture)
                    ?? throw new InvalidOperationException("PostgreSQL role command could not be generated.");
            }

            await using (var command = target.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = roleSql;
                await command.ExecuteNonQueryAsync();
            }

            var appConnectionBuilder = new NpgsqlConnectionStringBuilder(target.ConnectionString);
            appConnectionBuilder.Username = BuildPoolerUsername(
                appConnectionBuilder.Username
                    ?? throw new InvalidOperationException("Admin connection is missing a username."));
            appConnectionBuilder.Password = password;
            var appConnection = appConnectionBuilder.ConnectionString;

            SaveUserSecret("ConnectionStrings:PostgreSqlApp", appConnection);
            await transaction.CommitAsync();
            Console.WriteLine("Restricted application role created and PostgreSqlApp saved to User Secrets.");
            return 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> GrantSiteAdminAsync(NpgsqlConnection target, string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty.");
        }

        await using var findCommand = target.CreateCommand();
        findCommand.CommandText = $"""
            SELECT "Id", "IsAdmin"
            FROM "{TargetSchema}"."Users"
            WHERE "Username" = @username;
            """;
        findCommand.Parameters.AddWithValue("username", username);

        int userId;
        bool isAdmin;
        await using (var reader = await findCommand.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException($"User '{username}' was not found.");
            }

            userId = reader.GetInt32(0);
            isAdmin = reader.GetBoolean(1);
        }

        if (!isAdmin)
        {
            await using var updateCommand = target.CreateCommand();
            updateCommand.CommandText = $"""
                UPDATE "{TargetSchema}"."Users"
                SET "IsAdmin" = TRUE
                WHERE "Id" = @id AND "IsAdmin" = FALSE;
                """;
            updateCommand.Parameters.AddWithValue("id", userId);
            var changed = await updateCommand.ExecuteNonQueryAsync();
            if (changed != 1)
            {
                throw new InvalidOperationException("Admin permission update did not affect exactly one user.");
            }
        }

        await using var verifyCommand = target.CreateCommand();
        verifyCommand.CommandText = $"""
            SELECT "IsAdmin" FROM "{TargetSchema}"."Users" WHERE "Id" = @id;
            """;
        verifyCommand.Parameters.AddWithValue("id", userId);
        var verified = Convert.ToBoolean(await verifyCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        Console.WriteLine($"Site admin verified: username={username}, id={userId}, isAdmin={verified}");
        return verified ? 0 : 6;
    }

    private static string BuildPoolerUsername(string adminUsername)
    {
        var separatorIndex = adminUsername.IndexOf('.');
        return separatorIndex < 0
            ? AppRole
            : $"{AppRole}{adminUsername[separatorIndex..]}";
    }

    private static void SaveUserSecret(string key, string value)
    {
        var secretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            UserSecretsId,
            "secrets.json");

        Directory.CreateDirectory(Path.GetDirectoryName(secretsPath)!);
        var secrets = File.Exists(secretsPath)
            ? JsonNode.Parse(File.ReadAllText(secretsPath))?.AsObject() ?? new JsonObject()
            : new JsonObject();
        secrets[key] = value;

        var temporaryPath = secretsPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            secrets.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, secretsPath, overwrite: true);
    }

    private static async Task<List<string?[]>> ReadNormalizedRowsAsync(DbCommand command, TableSpec table)
    {
        var rows = new List<string?[]>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new string?[table.AllColumns.Count];
            for (var index = 0; index < table.AllColumns.Count; index++)
            {
                row[index] = await reader.IsDBNullAsync(index)
                    ? null
                    : NormalizeForDigest(reader.GetValue(index), table.AllColumns[index].Type);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task<long> CopyTableAsync(
        SqlConnection source,
        NpgsqlConnection target,
        NpgsqlTransaction transaction,
        TableSpec table)
    {
        await using var sourceCommand = source.CreateCommand();
        sourceCommand.CommandText = BuildSourceSelect(table);
        await using var reader = await sourceCommand.ExecuteReaderAsync();

        await using var importer = await target.BeginBinaryImportAsync(BuildCopyCommand(table));
        long copied = 0;

        while (await reader.ReadAsync())
        {
            await importer.StartRowAsync();
            for (var index = 0; index < table.AllColumns.Count; index++)
            {
                if (await reader.IsDBNullAsync(index))
                {
                    await importer.WriteNullAsync();
                    continue;
                }

                var column = table.AllColumns[index];
                var value = NormalizeForPostgres(reader.GetValue(index), column.Type);
                await importer.WriteAsync(value, column.Type);
            }

            copied++;
        }

        await importer.CompleteAsync();
        return copied;
    }

    private static object NormalizeForPostgres(object value, NpgsqlDbType type) => type switch
    {
        NpgsqlDbType.Integer => Convert.ToInt32(value, CultureInfo.InvariantCulture),
        NpgsqlDbType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
        NpgsqlDbType.Date => value is DateOnly dateOnly
            ? dateOnly
            : DateOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture)),
        NpgsqlDbType.Timestamp => DateTime.SpecifyKind(
            Convert.ToDateTime(value, CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static async Task ResetIdentityAsync(
        NpgsqlConnection target,
        NpgsqlTransaction transaction,
        TableSpec table)
    {
        EnsureWhitelisted(table);
        await using var command = target.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT setval(
                pg_get_serial_sequence('"{TargetSchema}"."{table.Name}"', 'Id'),
                COALESCE(MAX("Id"), 1),
                MAX("Id") IS NOT NULL)
            FROM "{TargetSchema}"."{table.Name}";
            """;
        await command.ExecuteScalarAsync();
    }

    private static async Task<(long Count, string Digest)> DigestSourceAsync(
        SqlConnection source,
        TableSpec table)
    {
        await using var command = source.CreateCommand();
        command.CommandText = BuildSourceSelect(table);
        return await ComputeDigestAsync(command, table);
    }

    private static async Task<(long Count, string Digest)> DigestTargetAsync(
        NpgsqlConnection target,
        TableSpec table)
    {
        await using var command = target.CreateCommand();
        command.CommandText = BuildTargetSelect(table);
        return await ComputeDigestAsync(command, table);
    }

    private static async Task<(long Count, string Digest)> ComputeDigestAsync(
        DbCommand command,
        TableSpec table)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var reader = await command.ExecuteReaderAsync();
        long count = 0;
        var lengthBuffer = new byte[4];

        while (await reader.ReadAsync())
        {
            AppendByte(hash, 0x1e);
            for (var index = 0; index < table.AllColumns.Count; index++)
            {
                if (await reader.IsDBNullAsync(index))
                {
                    AppendByte(hash, 0x00);
                    continue;
                }

                AppendByte(hash, 0x01);
                var normalized = NormalizeForDigest(reader.GetValue(index), table.AllColumns[index].Type);
                var bytes = Encoding.UTF8.GetBytes(normalized);
                BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, bytes.Length);
                hash.AppendData(lengthBuffer);
                hash.AppendData(bytes);
            }

            count++;
        }

        return (count, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static string NormalizeForDigest(object value, NpgsqlDbType type) => type switch
    {
        NpgsqlDbType.Integer => Convert.ToInt64(value, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture),
        NpgsqlDbType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? "1" : "0",
        NpgsqlDbType.Date => value switch
        {
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        },
        NpgsqlDbType.Timestamp => NormalizeTimestampForDigest(value),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static string NormalizeTimestampForDigest(object value)
    {
        var timestamp = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        var microsecondTicks = timestamp.Ticks - timestamp.Ticks % 10;
        return new DateTime(microsecondTicks, DateTimeKind.Unspecified)
            .ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountSourceAsync(SqlConnection source, TableSpec table)
    {
        EnsureWhitelisted(table);
        await using var command = source.CreateCommand();
        command.CommandText = $"SELECT COUNT_BIG(*) FROM [dbo].[{table.Name}];";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<long?> CountTargetIfPresentAsync(NpgsqlConnection target, TableSpec table)
    {
        EnsureWhitelisted(table);
        await using var exists = target.CreateCommand();
        exists.CommandText = "SELECT to_regclass(@qualified_name) IS NOT NULL;";
        exists.Parameters.AddWithValue("qualified_name", $"\"{TargetSchema}\".\"{table.Name}\"");
        if (!Convert.ToBoolean(await exists.ExecuteScalarAsync(), CultureInfo.InvariantCulture))
        {
            return null;
        }

        await using var command = target.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{TargetSchema}\".\"{table.Name}\";";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountTargetAsync(
        NpgsqlConnection target,
        NpgsqlTransaction transaction,
        TableSpec table)
    {
        EnsureWhitelisted(table);
        await using var command = target.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM \"{TargetSchema}\".\"{table.Name}\";";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TargetSchemaExistsAsync(NpgsqlConnection target)
    {
        await using var command = target.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema);";
        command.Parameters.AddWithValue("schema", TargetSchema);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountTargetOrphansAsync(NpgsqlConnection target)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM "petwork"."Answers" c LEFT JOIN "petwork"."Questions" p ON p."Id"=c."QuestionId" WHERE p."Id" IS NULL) +
                (SELECT COUNT(*) FROM "petwork"."Answers" c LEFT JOIN "petwork"."Users" p ON p."Id"=c."UserId" WHERE p."Id" IS NULL) +
                (SELECT COUNT(*) FROM "petwork"."BadgeUser" c LEFT JOIN "petwork"."Badges" p ON p."Id"=c."BadgesId" WHERE p."Id" IS NULL) +
                (SELECT COUNT(*) FROM "petwork"."BadgeUser" c LEFT JOIN "petwork"."Users" p ON p."Id"=c."UsersId" WHERE p."Id" IS NULL) +
                (SELECT COUNT(*) FROM "petwork"."BlogPosts" c LEFT JOIN "petwork"."Users" p ON p."Id"=c."UserId" WHERE p."Id" IS NULL) +
                (SELECT COUNT(*) FROM "petwork"."Guides" c LEFT JOIN "petwork"."Users" p ON p."Id"=c."UserId" WHERE p."Id" IS NULL) +
                (SELECT COUNT(*) FROM "petwork"."Pets" c LEFT JOIN "petwork"."Users" p ON p."Id"=c."UserId" WHERE p."Id" IS NULL) +
                (SELECT COUNT(*) FROM "petwork"."Questions" c LEFT JOIN "petwork"."Users" p ON p."Id"=c."UserId" WHERE p."Id" IS NULL) +
                (SELECT COUNT(*) FROM "petwork"."Recipes" c LEFT JOIN "petwork"."Users" p ON p."Id"=c."UserId" WHERE p."Id" IS NULL);
            """;

        await using var command = target.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static string BuildSourceSelect(TableSpec table)
    {
        EnsureWhitelisted(table);
        var columns = string.Join(", ", table.AllColumns.Select(column => $"[{column.Name}]"));
        var ordering = string.Join(", ", table.KeyColumns.Select(column => $"[{column}]"));
        return $"SELECT {columns} FROM [dbo].[{table.Name}] ORDER BY {ordering};";
    }

    private static string BuildTargetSelect(TableSpec table)
    {
        EnsureWhitelisted(table);
        var columns = string.Join(", ", table.AllColumns.Select(column => $"\"{column.Name}\""));
        var ordering = string.Join(", ", table.KeyColumns.Select(column => $"\"{column}\""));
        return $"SELECT {columns} FROM \"{TargetSchema}\".\"{table.Name}\" ORDER BY {ordering};";
    }

    private static string BuildCopyCommand(TableSpec table)
    {
        EnsureWhitelisted(table);
        var columns = string.Join(", ", table.AllColumns.Select(column => $"\"{column.Name}\""));
        return $"COPY \"{TargetSchema}\".\"{table.Name}\" ({columns}) FROM STDIN (FORMAT BINARY)";
    }

    private static void EnsureWhitelisted(TableSpec table)
    {
        if (!Tables.Any(approved => ReferenceEquals(approved, table)))
            throw new InvalidOperationException("Dynamic SQL identifiers must come from the approved table catalog.");
    }

    private static void AppendByte(IncrementalHash hash, byte value)
    {
        Span<byte> oneByte = stackalloc byte[1];
        oneByte[0] = value;
        hash.AppendData(oneByte);
    }

    private static ColumnSpec Text(string name) => new(name, NpgsqlDbType.Text);
    private static ColumnSpec Integer(string name) => new(name, NpgsqlDbType.Integer);
    private static ColumnSpec Boolean(string name) => new(name, NpgsqlDbType.Boolean);
    private static ColumnSpec Timestamp(string name) => new(name, NpgsqlDbType.Timestamp);
    private static ColumnSpec Date(string name) => new(name, NpgsqlDbType.Date);
}

internal sealed record ColumnSpec(string Name, NpgsqlDbType Type);

internal sealed record TableSpec(
    string Name,
    bool HasIdentity,
    IReadOnlyList<ColumnSpec> Columns,
    IReadOnlyList<string>? CompositeKey = null)
{
    public IReadOnlyList<ColumnSpec> AllColumns { get; } = HasIdentity
        ? [new ColumnSpec("Id", NpgsqlDbType.Integer), .. Columns]
        : Columns;

    public IReadOnlyList<string> KeyColumns { get; } = HasIdentity
        ? ["Id"]
        : CompositeKey ?? throw new ArgumentException("Composite key is required when a table has no identity.");
}

internal sealed class SecretMarker;
