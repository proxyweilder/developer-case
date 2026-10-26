using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Case1.Domain;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Case1.Api.Infrastructure;

public sealed class OrderStore(Database database)
{
    public async Task<(Order Order, bool Replay)> Create(string? requestKey, OrderInput input, CancellationToken ct)
    {
        if (requestKey is null || !Regex.IsMatch(requestKey, "^[A-Za-z0-9_-]{1,100}$"))
            throw new RuleViolation(400, "istek_anahtari_gerekli", "Idempotency-Key başlığında 1–100 harf, rakam, tire veya alt çizgi gönderin.");
        input = OrderRules.Normalize(input);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        await using var db = await database.Open(ct);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
        // This lock serializes only matching request keys, across all API instances.
        var acquired = await db.ExecuteScalarAsync<int>(Sql.Command(OrderQueries.AcquireRequestLock, new { resource = "order:" + requestKey }, tx, ct));
        if (acquired < 0) throw new RuleViolation(503, "istek_mesgul", "Aynı istek hâlâ işleniyor; aynı anahtarla tekrar deneyin.");
        var previous = await db.QuerySingleOrDefaultAsync<PreviousOrder>(Sql.Command(
         OrderQueries.PreviousOrder, new { requestKey }, tx, ct));
        if (previous is not null)
        {
            if (previous.RequestHash != hash)
                throw new RuleViolation(409, "anahtar_farkli_istekte_kullanildi", "Bu Idempotency-Key farklı bir siparişte kullanılmış. Yeni sipariş için yeni anahtar gönderin.");
            var replay = await Read(db, tx, previous.Id, ct);
            await tx.CommitAsync(ct);
            return (replay, true);
        }
        var reservations = new List<OrderLine>();
        foreach (var line in input.Satirlar)
        {
            var stock = await db.QuerySingleOrDefaultAsync<Stock>(Sql.Command(OrderQueries.StockForUpdate, new { code = line.UrunKod }, tx, ct));
            if (stock is null) throw new RuleViolation(404, "urun_bulunamadi", $"Ürün bulunamadı: {line.UrunKod}");
            var quantity = OrderRules.Reserve(line.Miktar, stock.Kullanilabilir, input.StokTercihi);
            reservations.Add(new(line.UrunKod, line.Miktar, quantity, 0));
        }
        if (reservations.All(x => x.Rezerve == 0))
            throw new RuleViolation(409, "yetersiz_stok", "Hiçbir satır için kullanılabilir stok yok; sipariş oluşturulmadı.");
        var id = Guid.NewGuid();
        await db.ExecuteAsync(Sql.Command(OrderQueries.InsertOrder, new { id, requestKey, hash }, tx, ct));
        await db.ExecuteAsync(Sql.Command(OrderQueries.ReserveAndInsertLines,
            new { id, linesJson = JsonSerializer.Serialize(reservations) }, tx, ct));
        var order = await Read(db, tx, id, ct);
        await tx.CommitAsync(ct);
        return (order, false);
    }
    public async Task<Order> Get(Guid id, CancellationToken ct)
    {
        await using var db = await database.Open(ct);
        return await Read(db, null, id, ct);
    }
    public async Task<Stock> GetStock(string code, CancellationToken ct)
    {
        code = OrderRules.ProductCode(code);
        await using var db = await database.Open(ct);
        return await db.QuerySingleOrDefaultAsync<Stock>(Sql.Command(OrderQueries.Stock, new { code }, null, ct)) ?? throw new RuleViolation(404, "urun_bulunamadi", $"Ürün bulunamadı: {code}");
    }
    public async Task<Order> Cancel(Guid id, CancellationInput input, CancellationToken ct)
    {
        var requested = input.Satirlar is null ? null : OrderRules.Lines(input.Satirlar);
        await using var db = await database.Open(ct);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
        // Serialize cancellations of the same order. Product locks follow the creation order.
        var exists = await db.ExecuteScalarAsync<int>(Sql.Command(
         OrderQueries.LockOrderForCancellation, new { id }, tx, ct));
        if (exists == 0) throw new RuleViolation(404, "siparis_bulunamadi", "Sipariş bulunamadı.");
        var order = await Read(db, tx, id, ct);
        var changes = requested ?? order.Satirlar.Where(x => x.AktifRezervasyon > 0)
         .Select(x => new OrderLineInput(x.UrunKod, x.AktifRezervasyon)).ToList();
        if (changes.Count == 0) throw new RuleViolation(409, "siparis_iptal_edilmis", "Siparişte iptal edilebilecek aktif rezervasyon kalmadı.");
        // SQL collation order may differ from ordinal order; use the creation order here too.
        foreach (var line in changes.OrderBy(x => x.UrunKod, StringComparer.Ordinal))
        {
            var original = order.Satirlar.SingleOrDefault(x => x.UrunKod == line.UrunKod)
             ?? throw new RuleViolation(404, "siparis_satiri_bulunamadi", $"Bu siparişte {line.UrunKod} yok.");
            OrderRules.EnsureCancellation(line.Miktar, original.AktifRezervasyon);
            await db.ExecuteAsync(Sql.Command(OrderQueries.CancelLine, new { id, code = line.UrunKod, quantity = line.Miktar }, tx, ct));
        }
        var updated = await Read(db, tx, id, ct);
        await tx.CommitAsync(ct);
        return updated;
    }
    private static async Task<Order> Read(SqlConnection db, SqlTransaction? tx, Guid id, CancellationToken ct)
    {
        // One round trip retrieves the header and all lines; no per-line read query.
        using var results = await db.QueryMultipleAsync(Sql.Command(OrderQueries.ReadOrder, new { id }, tx, ct));
        var date = await results.ReadSingleOrDefaultAsync<DateTime?>()
         ?? throw new RuleViolation(404, "siparis_bulunamadi", "Sipariş bulunamadı.");
        var lines = (await results.ReadAsync<OrderLine>()).ToList();
        var status = lines.All(x => x.AktifRezervasyon == 0) ? "iptalEdildi"
         : lines.Any(x => x.Iptal > 0) ? "kismenIptalEdildi"
         : lines.Any(x => x.Rezerve < x.Istenen) ? "kismenRezerveEdildi" : "rezerveEdildi";
        return new(id, status, DateTime.SpecifyKind(date, DateTimeKind.Utc), lines);
    }
    private sealed record PreviousOrder(Guid Id, string RequestHash);
}
