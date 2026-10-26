using Case1.Api.Infrastructure;
using Case1.Domain;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<ApiErrors>();
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<OrderStore>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("sql");
var app = builder.Build();
app.UseExceptionHandler();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { uygulama = "Case 1 — Sipariş ve stok", kilavuz = "README.md ve requests/case1.http" }));
app.MapPost("/siparisler", async (HttpContext http, OrderInput input, OrderStore store, CancellationToken ct) =>
{
    var result = await store.Create(http.Request.Headers["Idempotency-Key"].FirstOrDefault(), input, ct);
    http.Response.Headers["Idempotent-Replayed"] = result.Replay.ToString().ToLowerInvariant();
    return result.Replay ? Results.Ok(result.Order) : Results.Created($"/siparisler/{result.Order.Id}", result.Order);
});
app.MapGet("/siparisler/{id:guid}", async (Guid id, OrderStore store, CancellationToken ct) => Results.Ok(await store.Get(id, ct)));
app.MapGet("/urunler/{kod}/stok", async (string kod, OrderStore store, CancellationToken ct) => Results.Ok(await store.GetStock(kod, ct)));
app.MapPost("/siparisler/{id:guid}/iptal", async (Guid id, CancellationInput input, OrderStore store, CancellationToken ct) => Results.Ok(await store.Cancel(id, input, ct)));
await app.Services.GetRequiredService<Database>().Initialize(CancellationToken.None);
app.Run();
public partial class Program { }
