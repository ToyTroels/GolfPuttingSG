using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GolfSG.Database.Setup;

/// <summary>Explicit, transactional database updates; never run during API startup.</summary>
public static class DatabaseSetup
{
    public static async Task UpdateAsync(IConfiguration configuration, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("DatabaseSetup")
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DatabaseSetup or ConnectionStrings:Default.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        async Task Execute(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Serialize concurrent setup runs. The lock is released on commit or rollback.
        await Execute("SELECT pg_advisory_xact_lock(714051, 1)");
        await Execute("""
            CREATE TABLE IF NOT EXISTS public.golfsg_schema_versions (
                name TEXT PRIMARY KEY,
                sha256 TEXT NOT NULL,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);

        var assembly = typeof(DatabaseSetup).Assembly;
        const string prefix = "GolfSG.Database.Setup.Sql.Migrations.";
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var name = resource[prefix.Length..];
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            var sql = await reader.ReadToEndAsync(cancellationToken);
            // Normalize line endings so checkouts on Windows and Unix have the same checksum.
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql.Replace("\r\n", "\n"))));
            await using var lookup = new NpgsqlCommand(
                "SELECT sha256 FROM public.golfsg_schema_versions WHERE name = @name", connection, transaction);
            lookup.Parameters.AddWithValue("name", name);
            var existing = await lookup.ExecuteScalarAsync(cancellationToken);
            if (existing is string storedHash)
            {
                if (storedHash != hash)
                    throw new InvalidOperationException($"Applied migration {name} has changed. Restore it and add a new migration instead.");
                Console.WriteLine($"Already applied: {name}");
                continue;
            }

            await Execute(sql);
            await using var record = new NpgsqlCommand(
                "INSERT INTO public.golfsg_schema_versions (name, sha256) VALUES (@name, @hash)", connection, transaction);
            record.Parameters.AddWithValue("name", name);
            record.Parameters.AddWithValue("hash", hash);
            await record.ExecuteNonQueryAsync(cancellationToken);
            Console.WriteLine($"Prepared: {name}");
        }

        // Optional grants are reapplied on every run, including already-current databases.
        var role = configuration["DatabaseSetup:ApplicationRole"];
        if (!string.IsNullOrWhiteSpace(role))
        {
            using var commandBuilder = new NpgsqlCommandBuilder();
            var quotedRole = commandBuilder.QuoteIdentifier(role);
            await Execute($"GRANT USAGE ON SCHEMA public TO {quotedRole}");
            await Execute($"GRANT SELECT, INSERT, UPDATE, DELETE ON public.products TO {quotedRole}");
            await Execute($"GRANT SELECT, INSERT, UPDATE ON public.saved_rounds TO {quotedRole}");
            await Execute($"GRANT USAGE, SELECT ON SEQUENCE public.products_id_seq TO {quotedRole}");
            await Execute($"""
                GRANT SELECT, INSERT, UPDATE, DELETE ON
                    public."AspNetUsers", public."AspNetRoles", public."AspNetUserClaims",
                    public."AspNetRoleClaims", public."AspNetUserLogins", public."AspNetUserRoles",
                    public."AspNetUserTokens" TO {quotedRole}
                """);
            await Execute($"""
                GRANT USAGE, SELECT ON SEQUENCE public."AspNetUserClaims_Id_seq",
                    public."AspNetRoleClaims_Id_seq" TO {quotedRole}
                """);
        }

        await transaction.CommitAsync(cancellationToken);
        Console.WriteLine("Database setup committed successfully.");
    }
}
