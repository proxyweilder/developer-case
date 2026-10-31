using System.Net;
using System.Net.Http.Json;
using Case1.Domain;
using Dapper;
namespace Case1.Tests;
public class OrderIntegrationTests(SqlFixture sql) : IClassFixture<SqlFixture>
{
    [SqlTheory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"stokTercihi\":")]
    [InlineData("{\"stokTercihi\":\"tumunuReddet\",\"satirlar\":[null]}")]
    public async Task Invalid_json_returns_actionable_problem_without_creating_an_order(string body)
    {
        var key = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/siparisler")
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key);
        using var response = await sql.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var problem = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("kod").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
        await using var db = await sql.Open();
        Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Orders WHERE RequestKey=@key;", new { key }));
    }

    [SqlFact]
    public async Task Reversed_multi_product_race_reserves_both_products_for_only_one_order()
    {
        var first = await Product(1); var second = await Product(1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Send(bool reverse)
        {
            await gate.Task;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/siparisler")
            { Content = JsonContent.Create(new OrderInput("tumunuReddet", reverse
                ? [new(second, 1), new(first, 1)] : [new(first, 1), new(second, 1)])) };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            return await sql.Client.SendAsync(request);
        }
        var left = Send(false); var right = Send(true); gate.SetResult();
        var responses = await Task.WhenAll(left, right);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var code in new[] { first, second })
        {
            var stock = await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{code}/stok");
            Assert.Equal(1, stock!.Rezerve);
            Assert.Equal(0, stock.Kullanilabilir);
        }
    }

    [SqlFact]
    public async Task Normalized_retry_after_cancellation_returns_current_order_without_reserving_again()
    {
        var first = await Product(3); var second = await Product(3);
        var key = Guid.NewGuid().ToString("N");
        async Task<HttpResponseMessage> Send(List<OrderLineInput> lines)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/siparisler")
            { Content = JsonContent.Create(new OrderInput("tumunuReddet", lines)) };
            request.Headers.Add("Idempotency-Key", key);
            return await sql.Client.SendAsync(request);
        }
        using var initial = await Send([new(first, 1), new(second, 1)]);
        Assert.Equal(HttpStatusCode.Created, initial.StatusCode);
        var order = await initial.Content.ReadFromJsonAsync<Order>();
        using var cancelled = await sql.Client.PostAsJsonAsync($"/siparisler/{order!.Id}/iptal", new CancellationInput(null));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var replay = await Send([new(" " + second.ToLowerInvariant() + " ", 1), new(first.ToLowerInvariant(), 1)]);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        var current = await replay.Content.ReadFromJsonAsync<Order>();
        Assert.Equal(order.Id, current!.Id);
        Assert.Equal("iptalEdildi", current.Durum);
        foreach (var code in new[] { first, second })
            Assert.Equal(0, (await sql.Client.GetFromJsonAsync<Stock>($"/urunler/{code}/stok"))!.Rezerve);
    }

    [SqlFact]
    public async Task Partial_choice_with_no_available_stock_does_not_consume_request_key()
    {
        var code = await Product(0); var key = Guid.NewGuid().ToString("N");
        using var rejected = await Create(code, 1, key, "uygunMiktariAl");
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        await using var db = await sql.Open();
        Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Orders WHERE RequestKey=@key;", new { key }));
        await db.ExecuteAsync("UPDATE dbo.Products SET Physical=1 WHERE Code=@code;", new { code });
        using var retry = await Create(code, 1, key, "uygunMiktariAl");
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

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
