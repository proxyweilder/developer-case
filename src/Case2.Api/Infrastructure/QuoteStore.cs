using Case2.Domain;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Case2.Api.Infrastructure;
public sealed class QuoteStore(Database database)
{
    public async Task<Quote> Create(QuoteInput input, CancellationToken ct)
    {
        QuoteRules.Validate(input);
        await using var db = await database.Open(ct);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
        var customer = await db.QuerySingleOrDefaultAsync<Customer>(Sql.Command(QuoteQueries.CustomerForUpdate, new { code = input.CariKod }, tx, ct))
         ?? throw new RuleViolation(404, "cari_bulunamadi", "Cari bulunamadı. Cari kodunu kontrol edin.");
        var amounts = QuoteRules.Calculate(customer.Grup, input.Satirlar);
        QuoteRules.CheckCredit(customer, amounts, input.KrediLimitiAsiminiOnayla);
        // Insert and balance update commit together. Decimal parameters are culture-independent.
        var id = await db.ExecuteScalarAsync<int>(Sql.Command(QuoteQueries.SaveQuoteAndBalance, new { code = customer.CariKod, total = amounts.Tutar, withTax = amounts.KdvliTutar }, tx, ct));
        await tx.CommitAsync(ct);
        return new(id, customer.CariKod, amounts.Tutar, amounts.KdvliTutar, customer.Bakiye + amounts.KdvliTutar,
         customer.Bakiye + amounts.HesaplananKdvliTutar > customer.KrediLimiti && input.KrediLimitiAsiminiOnayla);
    }
}
