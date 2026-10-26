using System.Net;
using System.Net.Http.Json;
using Case1.Domain;
using Dapper;
namespace Case1.Tests;
public class OrderIntegrationTests(SqlFixture sql) : IClassFixture<SqlFixture>
{
    private async Task<string> Product(int stock)
    {
        var code = "T-" + Guid.NewGuid().ToString("N");
        await using var db = await sql.Open();
        await db.ExecuteAsync("INSERT dbo.Products(Code,Physical) VALUES(@code,@stock);", new { code, stock });
        return code;
    }
    private Task<HttpResponseMessage> Create(string code, int quantity, string? key = null, string choice = "tumunuReddet")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/siparisler")
        { Content = JsonContent.Create(new OrderInput(choice, [new(code, quantity)])) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return sql.Client.SendAsync(request);
    }
    [SqlFact]
    public async Task Two_simultaneous_orders_for_last_unit_have_exactly_one_winner()
    {
        var code = await Product(1);
        // Release both requests through the same gate, rather than await them in sequence.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Race() { await gate.Task; return await Create(code, 1); }
        var first = Race(); var second = Race(); gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var stock = await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{code}/stok");
        Assert.Equal(0, stock!.Kullanilabilir);
        Assert.Equal(1, stock.Rezerve);
    }
    [SqlFact]
    public async Task Concurrent_retries_create_one_order_and_one_reservation()
    {
        var code = await Product(10); var key = Guid.NewGuid().ToString("N");
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Create(code, 1, key)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        var orders = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<Order>()));
        Assert.Single(orders.Select(x => x!.Id).Distinct());
        var stock = await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{code}/stok");
        Assert.Equal(9, stock!.Kullanilabilir);
        Assert.Equal(HttpStatusCode.Conflict, (await Create(code, 2, key)).StatusCode);
    }
    [SqlFact]
    public async Task Partial_then_full_cancellation_returns_stock_once()
    {
        var code = await Product(10);
        var created = await (await Create(code, 3)).Content.ReadFromJsonAsync<Order>();
        var path = $"/siparisler/{created!.Id}/iptal";
        var partial = await sql.Client.PostAsJsonAsync(path, new CancellationInput([new(code, 1)]));
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var full = await sql.Client.PostAsJsonAsync(path, new CancellationInput(null));
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        var again = await sql.Client.PostAsJsonAsync(path, new CancellationInput(null));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(10, (await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{code}/stok"))!.Kullanilabilir);
    }
    [SqlFact]
    public async Task Concurrent_cancellations_cannot_release_the_same_unit_twice()
    {
        var code = await Product(1);
        var created = await (await Create(code, 1)).Content.ReadFromJsonAsync<Order>();
        var path = $"/siparisler/{created!.Id}/iptal";
        var results = await Task.WhenAll(
         sql.Client.PostAsJsonAsync(path, new CancellationInput(null)),
         sql.Client.PostAsJsonAsync(path, new CancellationInput(null)));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, (await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{code}/stok"))!.Kullanilabilir);
    }
    [SqlFact]
    public async Task Partial_choice_reserves_available_amount_and_reports_shortfall()
    {
        var code = await Product(2);
        var response = await Create(code, 5, choice: "uygunMiktariAl");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<Order>();
        Assert.Equal("kismenRezerveEdildi", order!.Durum);
        Assert.Equal(5, order.Satirlar[0].Istenen);
        Assert.Equal(2, order.Satirlar[0].Rezerve);
    }
    [SqlFact]
    public async Task Unknown_product_does_not_change_other_products()
    {
        var code = await Product(3);
        var request = new HttpRequestMessage(HttpMethod.Post, "/siparisler")
        { Content = JsonContent.Create(new OrderInput("tumunuReddet", [new(code, 1), new("ZZ-NONEXISTENT", 1)])) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NotFound, (await sql.Client.SendAsync(request)).StatusCode);
        Assert.Equal(3, (await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{code}/stok"))!.Kullanilabilir);
    }
    [SqlFact]
    public async Task Invalid_second_cancellation_rolls_back_first_stock_update()
    {
        var first = await Product(3); var second = await Product(1);
        var lines = new[] { first, second }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var request = new HttpRequestMessage(HttpMethod.Post, "/siparisler")
        { Content = JsonContent.Create(new OrderInput("tumunuReddet", [new(first, 1), new(second, 1)])) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var order = await (await sql.Client.SendAsync(request)).Content.ReadFromJsonAsync<Order>();
        var result = await sql.Client.PostAsJsonAsync($"/siparisler/{order!.Id}/iptal", new CancellationInput([new(lines[0], 1), new(lines[1], 2)]));
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        var unchanged = await sql.Client.GetFromJsonAsync<Order>($"/siparisler/{order.Id}");
        Assert.All(unchanged!.Satirlar, x => Assert.Equal(0, x.Iptal));
        var firstStock = await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{first}/stok");
        var secondStock = await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{second}/stok");
        Assert.Equal(1, firstStock!.Rezerve);
        Assert.Equal(2, firstStock.Kullanilabilir);
        Assert.Equal(1, secondStock!.Rezerve);
        Assert.Equal(0, secondStock.Kullanilabilir);
    }

    [SqlFact]
    public async Task Insufficient_second_product_rejects_entire_order_without_any_reservation()
    {
        var available = await Product(5);
        var unavailable = await Product(0);
        var requestKey = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/siparisler")
        {
            Content = JsonContent.Create(new OrderInput("tumunuReddet",
                [new(available, 2), new(unavailable, 1)]))
        };
        request.Headers.Add("Idempotency-Key", requestKey);

        using var response = await sql.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var firstStock = await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{available}/stok");
        var secondStock = await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{unavailable}/stok");
        Assert.Equal(0, firstStock!.Rezerve);
        Assert.Equal(5, firstStock.Kullanilabilir);
        Assert.Equal(0, secondStock!.Rezerve);
        await using var db = await sql.Open();
        Assert.Equal(0, await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Orders WHERE RequestKey=@requestKey;", new { requestKey }));
    }
}
