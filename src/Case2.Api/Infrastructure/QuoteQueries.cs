namespace Case2.Api.Infrastructure;
internal static class QuoteQueries
{
    // The update lock is retained until both the quote and balance update commit.
    public const string CustomerForUpdate = """
        SELECT CariKod, Grup, KrediLimiti, Bakiye
        FROM dbo.Cariler WITH (UPDLOCK, HOLDLOCK)
        WHERE CariKod = @code;
        """;

    public const string SaveQuoteAndBalance = """
        INSERT dbo.Teklifler (CariKod, Tutar, KdvliTutar, Tarih)
        OUTPUT INSERTED.Id
        VALUES (@code, @total, @withTax, GETDATE());

        UPDATE dbo.Cariler
        SET Bakiye = Bakiye + @withTax
        WHERE CariKod = @code;
        """;
}
