using Dapper;
using Microsoft.Data.SqlClient;
namespace Case1.Api.Infrastructure;

// Each operation owns its connection; pooling is handled by SqlClient.
public sealed class Database(IConfiguration configuration)
{
    public async Task<SqlConnection> Open(CancellationToken ct)
    {
        var connection = new SqlConnection(configuration.GetConnectionString("Database")
         ?? throw new InvalidOperationException("ConnectionStrings:Database ayarlanmalı."));
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    public async Task Initialize(CancellationToken ct)
    {
        var settings = new SqlConnectionStringBuilder(configuration.GetConnectionString("Database"));
        var name = settings.InitialCatalog;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_]+$"))
            throw new InvalidOperationException("Veritabanı adı yalnız harf, sayı ve alt çizgi içerebilir.");
        settings.InitialCatalog = "master";
        await using var master = new SqlConnection(settings.ConnectionString);
        await master.OpenAsync(ct);
        // Database identifier is validated above. All request values use SQL parameters.
        await master.ExecuteAsync(new CommandDefinition(
         $"IF DB_ID(@name) IS NULL CREATE DATABASE [{name}];", new { name }, cancellationToken: ct));
        await using var db = await Open(ct);
        var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql"), ct);
        await db.ExecuteAsync(new CommandDefinition(schema, cancellationToken: ct));
    }
}
internal static class Sql
{
    public static CommandDefinition Command(string query, object? args, SqlTransaction? transaction, CancellationToken ct)
     => new(query, args, transaction, commandTimeout: 15, cancellationToken: ct);
}
