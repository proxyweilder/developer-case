using Case2.Domain;
namespace Case2.Tests;
public class QuoteRulesTests
{
    [Theory]
    [InlineData("PERAKENDE", 49, 490)]
    [InlineData("PERAKENDE", 50, 475)]
    [InlineData("PERAKENDE", 99, 940.5)]
    [InlineData("PERAKENDE", 100, 950)]
    [InlineData("PERAKENDE", 101, 959.5)]
    [InlineData("BAYI", 49, 475.3)]
    [InlineData("BAYI", 50, 460)]
    [InlineData("BAYI", 99, 910.8)]
    [InlineData("BAYI", 100, 920)]
    public void Legacy_discount_boundaries_have_fixed_expected_results(string group, int quantity, decimal expected)
    {
        var amounts = QuoteRules.Calculate(group, [new(quantity, 10m)]);
        Assert.Equal(expected, amounts.Tutar);
        Assert.Equal(expected * 1.2m, amounts.HesaplananKdvliTutar);
    }
    [Fact]
    public void Each_line_is_rounded_with_legacy_bankers_rounding_before_sum()
    {
        var amounts = QuoteRules.Calculate("PERAKENDE", [new(1, 0.025m), new(1, 0.025m)]);
        Assert.Equal(0.04m, amounts.Tutar);
        Assert.Equal(0.048m, amounts.HesaplananKdvliTutar);
        Assert.Equal(0.05m, amounts.KdvliTutar);
    }
    [Fact]
    public void Credit_limit_equality_is_allowed()
     => QuoteRules.CheckCredit(new("C", "BAYI", 120, 0), new(100, 120, 120), false);
    [Fact]
    public void Credit_limit_uses_unrounded_legacy_tax_total()
    {
        var error = Assert.Throws<RuleViolation>(() => QuoteRules.CheckCredit(new("C", "BAYI", 0.01m, 0), new(0.01m, 0.01m, 0.012m), false));
        Assert.Equal("kredi_limiti_onayi_gerekli", error.Code);
    }
    [Fact]
    public void Explicit_confirmation_allows_limit_overrun()
     => QuoteRules.CheckCredit(new("C", "BAYI", 10, 0), new(100, 120, 120), true);
    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, -1)]
    public void Invalid_lines_are_rejected(int quantity, decimal price)
     => Assert.Throws<RuleViolation>(() => QuoteRules.Validate(new("C", [new(quantity, price)])));
}
