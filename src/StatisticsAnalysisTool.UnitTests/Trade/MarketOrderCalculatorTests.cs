using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Trade.Market;

namespace StatisticsAnalysisTool.UnitTests.Trade;

public class MarketOrderCalculatorTests
{
    [Test]
    public void Calculate_WithMaximumPrice_FiltersAndTotalsMatchingOrders()
    {
        MarketOrderRow[] orders =
        [
            CreateOrder(1, 100, 2),
            CreateOrder(2, 125, 3),
            CreateOrder(3, 150, 4)
        ];

        var result = MarketOrderCalculator.Calculate(orders, "125");

        result.IsLimitValid.Should().BeTrue();
        result.OrderCount.Should().Be(2);
        result.TotalAmount.Should().Be(5);
        result.TotalPrice.Should().Be(575);
    }

    [TestCase("125.000", 125000ul)]
    [TestCase("125,000", 125000ul)]
    [TestCase("125 000", 125000ul)]
    public void TryParseMaximumPrice_AcceptsCommonThousandsSeparators(string input, ulong expected)
    {
        var isValid = MarketOrderCalculator.TryParseMaximumPrice(input, out var result);

        isValid.Should().BeTrue();
        result.Should().Be(expected);
    }

    [Test]
    public void Calculate_WithInvalidMaximumPrice_ReturnsNoMatches()
    {
        var result = MarketOrderCalculator.Calculate([CreateOrder(1, 100, 2)], "12x");

        result.IsLimitValid.Should().BeFalse();
        result.OrderCount.Should().Be(0);
        result.TotalPrice.Should().Be(0);
    }

    [Test]
    public void Calculate_WithoutMaximumPrice_IncludesEveryOrder()
    {
        var result = MarketOrderCalculator.Calculate(
            [CreateOrder(1, 100, 2), CreateOrder(2, 150, 4)],
            string.Empty);

        result.IsLimitValid.Should().BeTrue();
        result.MaximumUnitPrice.Should().BeNull();
        result.OrderCount.Should().Be(2);
        result.TotalAmount.Should().Be(6);
        result.TotalPrice.Should().Be(800);
    }

    [Test]
    public void UpdateSellOrders_SortsCheapestFirstAndRemovesDuplicateOrderIds()
    {
        var bindings = new MarketOrdersBindings();
        AuctionEntry[] orders =
        [
            CreateAuctionEntry(1, "T4_BAG", 150, 1),
            CreateAuctionEntry(2, "T4_BAG", 100, 1),
            CreateAuctionEntry(1, "T4_BAG", 125, 2)
        ];

        bindings.UpdateSellOrders(orders, MarketLocation.LymhurstMarket);

        bindings.SellOrders.Select(x => x.UnitPrice).Should().Equal(100, 125);
        bindings.SellOrders.Single(x => x.Id == 1).Amount.Should().Be(2);
    }

    [Test]
    public void UpdateOrders_WhenItemChanges_ClearsOrdersFromPreviousItem()
    {
        var bindings = new MarketOrdersBindings();
        bindings.UpdateBuyOrders(
            [CreateAuctionEntry(1, "T4_BAG", 100, 1)],
            MarketLocation.LymhurstMarket);

        bindings.UpdateSellOrders(
            [CreateAuctionEntry(2, "T5_BAG", 200, 1)],
            MarketLocation.LymhurstMarket);

        bindings.BuyOrders.Should().BeEmpty();
        bindings.SellOrders.Should().ContainSingle();
        bindings.SellOrders[0].ItemTypeId.Should().Be("T5_BAG");
    }

    private static MarketOrderRow CreateOrder(long id, long unitPrice, int amount)
    {
        return new MarketOrderRow(CreateAuctionEntry(id, "T4_BAG", unitPrice, amount));
    }

    private static AuctionEntry CreateAuctionEntry(long id, string itemTypeId, long unitPrice, int amount)
    {
        return new AuctionEntry
        {
            Id = id,
            ItemTypeId = itemTypeId,
            QualityLevel = 1,
            Amount = amount,
            UnitPriceSilver = unitPrice * 10_000
        };
    }
}
