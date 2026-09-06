using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Trade;
using StatisticsAnalysisTool.Trade.Market;

namespace StatisticsAnalysisTool.UnitTests.Trade;

public class MarketActivityRowTests
{
    [Test]
    public void TryCreate_InstantBuy_ReportsPurchaseCostAndStoredBalance()
    {
        var trade = CreateInstantTrade(TradeType.InstantBuy, 100, 3, 0, 1_250_000);

        var created = MarketActivityRow.TryCreate(trade, out var row);

        created.Should().BeTrue();
        row.IsPurchase.Should().BeTrue();
        row.Quantity.Should().Be(3);
        row.UnitPrice.Should().Be(100);
        row.GrossTotal.Should().Be(300);
        row.BalanceChange.Should().Be(-300);
        row.BalanceAfter.Should().Be(1_250_000);
    }

    [Test]
    public void TryCreate_InstantSell_DeductsTaxFromBalanceEffect()
    {
        var trade = CreateInstantTrade(TradeType.InstantSell, 100, 3, 10, 2_000_270);

        var created = MarketActivityRow.TryCreate(trade, out var row);

        created.Should().BeTrue();
        row.IsSale.Should().BeTrue();
        row.GrossTotal.Should().Be(300);
        row.Fees.Should().Be(30);
        row.BalanceChange.Should().Be(270);
        row.BalanceAfter.Should().Be(2_000_270);
    }

    [Test]
    public void TradeMapping_PreservesBalanceAfter()
    {
        var trade = CreateInstantTrade(TradeType.InstantBuy, 250, 2, 0, 987_654);

        var restored = TradeMapping.Mapping(TradeMapping.Mapping(trade));

        restored.BalanceAfter.Should().Be(987_654);
    }

    private static StatisticsAnalysisTool.Trade.Trade CreateInstantTrade(
        TradeType type, long unitPrice, int quantity, double taxRate, long balanceAfter)
    {
        return new StatisticsAnalysisTool.Trade.Trade
        {
            Id = 1,
            Ticks = DateTime.UtcNow.Ticks,
            Type = type,
            ClusterIndex = "Bridgewatch",
            BalanceAfter = balanceAfter,
            AuctionEntry = new AuctionEntry
            {
                ItemTypeId = "T4_BUTTER",
                QualityLevel = 1
            },
            InstantBuySellContent = new InstantBuySellContent
            {
                InternalUnitPrice = unitPrice * 10_000,
                Quantity = quantity,
                TaxRate = taxRate
            }
        };
    }
}
