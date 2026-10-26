using System.Net;
using System.Net.Http.Json;
using Case2.Api.Infrastructure;
using Case2.Domain;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace Case2.Tests;

public class SqlReliabilityTests(SqlFixture sql) : IClassFixture<SqlFixture>
{
    [SqlFact]
    public async Task Decimal_rounding_is_consistent_in_response_quote_row_and_customer_balance()
    {
        var code = "M-" + Guid.NewGuid().ToString("N")[..16];
        await using var db = await sql.Open();
        await db.ExecuteAsync("INSERT dbo.Cariler VALUES(@code,N'PERAKENDE',100,0);", new { code });

        using var response = await sql.Client.PostAsJsonAsync("/teklifler",
            new QuoteInput(code, [new(1, 0.025m), new(1, 0.025m)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<Quote>();
        Assert.Equal(0.04m, quote!.Tutar);
        Assert.Equal(0.05m, quote.KdvliTutar);
        Assert.Equal(0.05m, quote.YeniBakiye);
        Assert.Equal(0.04m, await db.ExecuteScalarAsync<decimal>(
            "SELECT Tutar FROM dbo.Teklifler WHERE Id=@id;", new { quote.Id }));
        Assert.Equal(0.05m, await db.ExecuteScalarAsync<decimal>(
            "SELECT KdvliTutar FROM dbo.Teklifler WHERE Id=@id;", new { quote.Id }));
        Assert.Equal(0.05m, await db.ExecuteScalarAsync<decimal>(
            "SELECT Bakiye FROM dbo.Cariler WHERE CariKod=@code;", new { code }));
    }

    [SqlFact]
    public async Task Existing_desktop_schema_and_records_survive_repeated_initialization()
    {
        await using var db = await sql.Open();
        const string schemaQuery = """
            SELECT CONCAT(t.name,'.',c.name,':',ty.name,':',c.max_length,':',
                          c.precision,':',c.scale,':',c.is_nullable,':',c.is_identity)
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id=t.schema_id
            INNER JOIN sys.columns AS c ON c.object_id=t.object_id
            INNER JOIN sys.types AS ty ON ty.user_type_id=c.user_type_id
            WHERE s.name=N'dbo' AND t.name IN (N'Cariler',N'Teklifler')
            ORDER BY t.name,c.column_id;
            """;
        // These column contracts come from the supplied case document, not from schema.sql.
        string[] expected =
        [
            "Cariler.CariKod:nvarchar:40:0:0:0:0",
            "Cariler.Grup:nvarchar:20:0:0:0:0",
            "Cariler.KrediLimiti:decimal:9:18:2:0:0",
            "Cariler.Bakiye:decimal:9:18:2:0:0",
            "Teklifler.Id:int:4:10:0:0:1",
            "Teklifler.CariKod:nvarchar:40:0:0:0:0",
            "Teklifler.Tutar:decimal:9:18:2:0:0",
            "Teklifler.KdvliTutar:decimal:9:18:2:0:0",
            "Teklifler.Tarih:datetime:8:23:3:0:0"
        ];
        Assert.Equal(expected, (await db.QueryAsync<string>(schemaQuery)).ToArray());
        var code = "S-" + Guid.NewGuid().ToString("N")[..16];
        await db.ExecuteAsync("""
            INSERT dbo.Cariler VALUES(@code,N'BAYI',1000,12.34);
            INSERT dbo.Teklifler(CariKod,Tutar,KdvliTutar,Tarih)
            VALUES(@code,10,12,GETDATE());
            """, new { code });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Database"] = sql.ConnectionString }).Build();
        var database = new Database(configuration);

        await database.Initialize(CancellationToken.None);
        await database.Initialize(CancellationToken.None);

        Assert.Equal(expected, (await db.QueryAsync<string>(schemaQuery)).ToArray());
        Assert.Equal(12.34m, await db.ExecuteScalarAsync<decimal>(
            "SELECT Bakiye FROM dbo.Cariler WHERE CariKod=@code;", new { code }));
        Assert.Equal(1, await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Teklifler WHERE CariKod=@code;", new { code }));
    }
}
