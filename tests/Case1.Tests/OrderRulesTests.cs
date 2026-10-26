using Case1.Domain;
namespace Case1.Tests;
public class OrderRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000001)]
    public void Invalid_quantity_has_an_actionable_error(int quantity)
    {
        var error = Assert.Throws<RuleViolation>(() => OrderRules.Lines([new("URN-001", quantity)]));
        Assert.Equal(400, error.Status);
        Assert.Equal("gecersiz_miktar", error.Code);
    }
    [Fact]
    public void Product_codes_are_normalized_and_sorted()
    {
        var lines = OrderRules.Lines([new(" urn-002 ", 1), new("urn-001", 2)]);
        Assert.Equal(new[] { "URN-001", "URN-002" }, lines.Select(x => x.UrunKod));
    }
    [Fact]
    public void Duplicates_are_rejected_after_normalization()
     => Assert.Throws<RuleViolation>(() => OrderRules.Lines([new("urn-001", 1), new("URN-001", 1)]));
    [Fact]
    public void Reject_all_does_not_accept_insufficient_stock()
     => Assert.Throws<RuleViolation>(() => OrderRules.Reserve(2, 1, "tumunuReddet"));
    [Theory]
    [InlineData(5, 2, 2)]
    [InlineData(2, 5, 2)]
    [InlineData(3, 0, 0)]
    public void Partial_choice_reserves_only_available_quantity(int requested, int available, int expected)
     => Assert.Equal(expected, OrderRules.Reserve(requested, available, "uygunMiktariAl"));
    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    public void Cancellation_cannot_return_more_than_remaining(int quantity, int remaining)
     => Assert.Throws<RuleViolation>(() => OrderRules.EnsureCancellation(quantity, remaining));
}
