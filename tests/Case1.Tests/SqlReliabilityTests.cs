using Case1.Api.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Case1.Tests;

public class SqlReliabilityTests(SqlFixture sql) : IClassFixture<SqlFixture>
{
    [SqlFact]
    public async Task Database_rejects_negative_or_excess_reserved_stock_even_without_the_api()
    {
        await using var db = await sql.Open();
        var code = "CHECK-" + Guid.NewGuid().ToString("N");
        await db.ExecuteAsync("INSERT dbo.Products(Code, Physical) VALUES(@code, 2);", new { code });

        foreach (var invalidReserved in new[] { -1, 3 })
        {
            var exception = await Assert.ThrowsAsync<SqlException>(() => db.ExecuteAsync(
                "UPDATE dbo.Products SET Reserved=@invalidReserved WHERE Code=@code;",
                new { code, invalidReserved }));
            Assert.Equal(547, exception.Number);
            Assert.Equal(0, await db.ExecuteScalarAsync<int>(
                "SELECT Reserved FROM dbo.Products WHERE Code=@code;", new { code }));
        }
    }

    [SqlFact]
    public async Task Repeated_initialization_preserves_existing_stock_and_seed_rows()
    {
        await using var db = await sql.Open();
        await db.ExecuteAsync("UPDATE dbo.Products SET Reserved=2 WHERE Code=N'URN-001';");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Database"] = sql.ConnectionString }).Build();
        var database = new Database(configuration);

        await database.Initialize(CancellationToken.None);
        await database.Initialize(CancellationToken.None);

        Assert.Equal(2, await db.ExecuteScalarAsync<int>(
            "SELECT Reserved FROM dbo.Products WHERE Code=N'URN-001';"));
        Assert.Equal(10, await db.ExecuteScalarAsync<int>(
            "SELECT Physical FROM dbo.Products WHERE Code=N'URN-001';"));
        Assert.Equal(3, await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Products WHERE Code IN (N'URN-001',N'URN-002',N'URN-003');"));
    }
}
