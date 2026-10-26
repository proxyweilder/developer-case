using Case2.Api.Infrastructure;
using Case2.Domain;
using Dapper;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<ApiErrors>();
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<QuoteStore>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("sql");
var app = builder.Build();
app.UseExceptionHandler();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { uygulama = "Case 2 — Teklif", kilavuz = "README.md ve requests/case2.http" }));
app.MapPost("/teklifler", async (QuoteInput input, QuoteStore store, CancellationToken ct) =>
 Results.Json(await store.Create(input, ct), statusCode: 201));
var database = app.Services.GetRequiredService<Database>();
await database.Initialize(CancellationToken.None);
if (app.Configuration.GetValue<bool>("SeedDemoData"))
{
    await using var db = await database.Open(CancellationToken.None);
    await db.ExecuteAsync("""
  IF NOT EXISTS(SELECT 1 FROM dbo.Cariler WHERE CariKod=N'CARI-001')
   INSERT dbo.Cariler VALUES(N'CARI-001',N'BAYI',100000,0);
  IF NOT EXISTS(SELECT 1 FROM dbo.Cariler WHERE CariKod=N'CARI-002')
   INSERT dbo.Cariler VALUES(N'CARI-002',N'PERAKENDE',100,0);
  """);
}
app.Run();
public partial class Program { }
