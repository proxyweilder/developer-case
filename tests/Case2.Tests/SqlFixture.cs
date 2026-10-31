using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
namespace Case2.Tests;

// Real SQL tests are explicit skips when SQL_TEST_CONNECTION is not configured.
public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQL_TEST_CONNECTION")))
            Skip = "SQL_TEST_CONNECTION ayarlı değil; gerçek SQL Server gerekli.";
    }
}
public sealed class SqlTheoryAttribute : TheoryAttribute
{
    public SqlTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQL_TEST_CONNECTION")))
            Skip = "SQL_TEST_CONNECTION ayarlı değil; gerçek SQL Server gerekli.";
    }
}
public sealed class SqlFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? factory;
    public HttpClient Client { get; private set; } = null!;
    public string ConnectionString { get; private set; } = "";
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("SQL_TEST_CONNECTION");
        if (string.IsNullOrEmpty(source)) return;
        var settings = new SqlConnectionStringBuilder(source) { InitialCatalog = "Case2Tests_" + Guid.NewGuid().ToString("N") };
        ConnectionString = settings.ConnectionString;
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
         web.UseEnvironment("Testing").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
          new Dictionary<string, string?> { ["ConnectionStrings:Database"] = ConnectionString, ["SeedDemoData"] = "false" })));
        Client = factory.CreateClient();
        await Task.CompletedTask;
    }
    public async Task<SqlConnection> Open()
    {
        var db = new SqlConnection(ConnectionString);
        await db.OpenAsync();
        return db;
    }
    public async Task DisposeAsync()
    {
        if (factory is null) return;
        Client.Dispose();
        await factory.DisposeAsync();
        SqlConnection.ClearAllPools();
        var settings = new SqlConnectionStringBuilder(ConnectionString);
        var name = settings.InitialCatalog;
        settings.InitialCatalog = "master";
        await using var db = new SqlConnection(settings.ConnectionString);
        await db.OpenAsync();
        // Generated test database name only; never target a supplied database for cleanup.
        if (!name.StartsWith("Case2Tests_") || name.Length != 43)
            throw new InvalidOperationException("Beklenmeyen test veritabanı adı.");
        await db.ExecuteAsync($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");
    }
}
