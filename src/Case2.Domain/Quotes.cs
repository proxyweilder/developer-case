namespace Case2.Domain;
public sealed record QuoteLine(int Miktar, decimal BirimFiyat);
public sealed record QuoteInput(string CariKod, List<QuoteLine> Satirlar, bool KrediLimitiAsiminiOnayla = false);
public sealed record Customer(string CariKod, string Grup, decimal KrediLimiti, decimal Bakiye);
public sealed record QuoteAmounts(decimal Tutar, decimal KdvliTutar, decimal HesaplananKdvliTutar);
public sealed record Quote(int Id, string CariKod, decimal Tutar, decimal KdvliTutar, decimal YeniBakiye, bool LimitAsimiOnaylandi);
public static class QuoteRules
{
    public static void Validate(QuoteInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CariKod) || input.CariKod.Length > 20)
            throw new RuleViolation(400, "gecersiz_cari_kod", "1–20 karakter uzunluğunda cariKod gönderin.");
        if (input.Satirlar is null || input.Satirlar.Count is < 1 or > 100)
            throw new RuleViolation(400, "gecersiz_satirlar", "1 ile 100 arasında teklif satırı gönderin.");
        if (input.Satirlar.Any(x => x is null || x.Miktar is < 1 or > 1000000 || x.BirimFiyat < 0 || x.BirimFiyat > 1000000000m))
            throw new RuleViolation(400, "gecersiz_satir", "Miktar 1–1000000, birim fiyat 0–1000000000 arasında olmalıdır.");
    }
    public static QuoteAmounts Calculate(string group, IReadOnlyList<QuoteLine> lines)
    {
        decimal total = 0;
        foreach (var line in lines)
        {
            // Preserve the legacy result: the >=100 branch is unreachable in the original code.
            var discount = line.Miktar >= 50 ? 0.05m : 0m;
            if (group == "BAYI") discount += 0.03m;
            total += Math.Round(line.Miktar * line.BirimFiyat * (1 - discount), 2, MidpointRounding.ToEven);
        }
        var rawWithTax = total * 1.2m;
        // SQL Server DECIMAL(18,2) conversion rounds midpoint away from zero.
        var storedWithTax = Math.Round(rawWithTax, 2, MidpointRounding.AwayFromZero);
        if (total > 9999999999999999.99m || storedWithTax > 9999999999999999.99m)
            throw new RuleViolation(400, "tutar_cok_buyuk", "Teklif tutarı veritabanındaki para alanına sığmıyor.");
        return new(total, storedWithTax, rawWithTax);
    }
    public static void CheckCredit(Customer customer, QuoteAmounts amounts, bool confirmed)
    {
        // The old code checks the unrounded VAT amount; keep that comparison.
        if (customer.Bakiye + amounts.HesaplananKdvliTutar > customer.KrediLimiti && !confirmed)
            throw new RuleViolation(409, "kredi_limiti_onayi_gerekli", "Kredi limiti aşılacak. Devam etmek istiyorsanız aynı bilgileri krediLimitiAsiminiOnayla: true ile gönderin.",
             new { customer.KrediLimiti, customer.Bakiye, teklifTutari = amounts.KdvliTutar, yeniBakiye = customer.Bakiye + amounts.KdvliTutar });
        if (customer.Bakiye + amounts.KdvliTutar > 9999999999999999.99m)
            throw new RuleViolation(400, "bakiye_cok_buyuk", "Yeni bakiye veritabanındaki para alanına sığmıyor.");
    }
}
