using System.Text.RegularExpressions;
namespace Case1.Domain;
public sealed record OrderLineInput(string UrunKod, int Miktar);
public sealed record OrderInput(string StokTercihi, List<OrderLineInput> Satirlar);
public sealed record CancellationInput(List<OrderLineInput>? Satirlar);
public sealed record Stock(string Kod, int Fiziksel, int Rezerve)
{
    public int Kullanilabilir => Fiziksel - Rezerve;
}
public sealed record OrderLine(string UrunKod, int Istenen, int Rezerve, int Iptal)
{
    public int AktifRezervasyon => Rezerve - Iptal;
}
public sealed record Order(Guid Id, string Durum, DateTime Tarih, IReadOnlyList<OrderLine> Satirlar);
public static class OrderRules
{
    public static string ProductCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? "";
        if (!Regex.IsMatch(normalized, "^[A-Z0-9-]{1,40}$"))
            throw new RuleViolation(400, "gecersiz_urun_kodu", "Ürün kodu 1–40 harf, rakam veya tire içermelidir.");
        return normalized;
    }
    public static OrderInput Normalize(OrderInput input)
    {
        if (input.StokTercihi is not ("tumunuReddet" or "uygunMiktariAl"))
            throw new RuleViolation(400, "gecersiz_stok_tercihi", "stokTercihi: tumunuReddet veya uygunMiktariAl olmalıdır.");
        return input with { Satirlar = Lines(input.Satirlar) };
    }
    public static List<OrderLineInput> Lines(List<OrderLineInput>? lines)
    {
        if (lines is null || lines.Count is < 1 or > 100)
            throw new RuleViolation(400, "gecersiz_satirlar", "1 ile 100 arasında sipariş satırı gönderin.");
        var result = new List<OrderLineInput>();
        foreach (var line in lines)
        {
            if (line is null || line.Miktar is < 1 or > 1000000)
                throw new RuleViolation(400, "gecersiz_miktar", "Her satırın miktarı 1 ile 1000000 arasında tam sayı olmalıdır.");
            result.Add(new(ProductCode(line.UrunKod), line.Miktar));
        }
        if (result.Select(x => x.UrunKod).Distinct().Count() != result.Count)
            throw new RuleViolation(400, "tekrarli_urun", "Bir ürünü tek satırda gönderin; miktarlarını birleştirin.");
        // All write paths acquire product locks in the same order.
        return result.OrderBy(x => x.UrunKod, StringComparer.Ordinal).ToList();
    }
    public static int Reserve(int requested, int available, string preference)
    {
        if (available < requested && preference == "tumunuReddet")
            throw new RuleViolation(409, "yetersiz_stok", "Siparişin tamamı için stok yetersiz; hiçbir rezervasyon yapılmadı.", new { istenen = requested, kullanilabilir = available });
        return Math.Min(requested, available);
    }
    public static void EnsureCancellation(int quantity, int remaining)
    {
        if (quantity <= 0 || quantity > remaining)
            throw new RuleViolation(409, "gecersiz_iptal", "İptal miktarı kalan rezervasyonu aşamaz. Tamamen iptal edilmiş satır tekrar iptal edilemez.", new { kalanRezervasyon = remaining });
    }
}
