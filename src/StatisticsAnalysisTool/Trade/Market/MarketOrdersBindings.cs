using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace StatisticsAnalysisTool.Trade.Market;

public sealed class MarketOrdersBindings : BaseViewModel
{
    private IReadOnlyList<MarketOrderRow> _capturedSellOrders = [];
    private IReadOnlyList<MarketOrderRow> _capturedBuyOrders = [];
    private string _currentItemTypeId = string.Empty;
    private MarketLocation _currentMarketLocation = MarketLocation.Unknown;

    public ObservableCollection<MarketOrderRow> SellOrders
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public ObservableCollection<MarketOrderRow> BuyOrders
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public string MaximumSellOrderPrice
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            RecalculateSellOrders();
        }
    } = string.Empty;

    public string MaximumBuyOrderPrice
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            RecalculateBuyOrders();
        }
    } = string.Empty;

    public string SellSummary
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Henüz sell order verisi alınmadı.";

    public string BuySummary
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Henüz buy order verisi alınmadı.";

    public string CurrentItemName
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Pazarda bir ürün görüntüleyin";

    public string CurrentMarketName
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Pazar bekleniyor";

    public string LastUpdatedText
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Takip aktifken oyun pazarından gelen emirler burada görünür.";

    public void UpdateSellOrders(IEnumerable<AuctionEntry> auctionEntries, MarketLocation marketLocation)
    {
        var rows = CreateRows(auctionEntries)
            .OrderBy(x => x.UnitPrice)
            .ThenBy(x => x.Expires)
            .ToList();

        _ = RunOnUiThreadAsync(() =>
        {
            PrepareForBatch(rows, marketLocation);
            _capturedSellOrders = rows;
            RecalculateSellOrders();
            UpdateHeader(rows, marketLocation);
        });
    }

    public void UpdateBuyOrders(IEnumerable<AuctionEntry> auctionEntries, MarketLocation marketLocation)
    {
        var rows = CreateRows(auctionEntries)
            .OrderByDescending(x => x.UnitPrice)
            .ThenBy(x => x.Expires)
            .ToList();

        _ = RunOnUiThreadAsync(() =>
        {
            PrepareForBatch(rows, marketLocation);
            _capturedBuyOrders = rows;
            RecalculateBuyOrders();
            UpdateHeader(rows, marketLocation);
        });
    }

    private void PrepareForBatch(IReadOnlyCollection<MarketOrderRow> rows, MarketLocation marketLocation)
    {
        var itemTypeIds = rows
            .Select(x => x.ItemTypeId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();
        var batchItemTypeId = itemTypeIds.Count == 1 ? itemTypeIds[0] : string.Empty;

        var itemChanged = !string.IsNullOrEmpty(batchItemTypeId)
                          && !string.IsNullOrEmpty(_currentItemTypeId)
                          && !string.Equals(batchItemTypeId, _currentItemTypeId, StringComparison.Ordinal);
        var marketChanged = marketLocation != MarketLocation.Unknown
                            && _currentMarketLocation != MarketLocation.Unknown
                            && marketLocation != _currentMarketLocation;

        if (itemChanged || marketChanged)
        {
            _capturedSellOrders = [];
            _capturedBuyOrders = [];
            SellOrders = [];
            BuyOrders = [];
            SellSummary = "Bu ürün için sell order verisi bekleniyor.";
            BuySummary = "Bu ürün için buy order verisi bekleniyor.";
        }

        if (!string.IsNullOrEmpty(batchItemTypeId))
        {
            _currentItemTypeId = batchItemTypeId;
        }

        if (marketLocation != MarketLocation.Unknown)
        {
            _currentMarketLocation = marketLocation;
        }
    }

    private void UpdateHeader(IReadOnlyCollection<MarketOrderRow> rows, MarketLocation marketLocation)
    {
        var itemTypeIds = rows.Select(x => x.ItemTypeId).Distinct(StringComparer.Ordinal).Take(2).ToList();
        if (itemTypeIds.Count == 1)
        {
            CurrentItemName = ItemController.GetItemByUniqueName(itemTypeIds[0])?.LocalizedName ?? itemTypeIds[0];
        }
        else if (itemTypeIds.Count > 1)
        {
            CurrentItemName = "Birden fazla ürün";
        }

        CurrentMarketName = Locations.GetDisplayName(marketLocation) ?? marketLocation.ToString();
        LastUpdatedText = $"Son veri: {DateTime.Now:G}";
    }

    private void RecalculateSellOrders()
    {
        var calculation = MarketOrderCalculator.Calculate(_capturedSellOrders, MaximumSellOrderPrice);
        SellOrders = new ObservableCollection<MarketOrderRow>(calculation.MatchingOrders);
        SellSummary = BuildSummary(calculation, "sell");
    }

    private void RecalculateBuyOrders()
    {
        var calculation = MarketOrderCalculator.Calculate(_capturedBuyOrders, MaximumBuyOrderPrice);
        BuyOrders = new ObservableCollection<MarketOrderRow>(calculation.MatchingOrders);
        BuySummary = BuildSummary(calculation, "buy");
    }

    private static string BuildSummary(MarketOrderCalculation calculation, string orderType)
    {
        if (!calculation.IsLimitValid)
        {
            return "Geçerli bir maksimum fiyat girin.";
        }

        var culture = CultureInfo.CurrentCulture;
        var prefix = calculation.MaximumUnitPrice.HasValue
            ? $"{calculation.MaximumUnitPrice.Value.ToString("N0", culture)} silver ve altı"
            : $"Tüm {orderType} order kayıtları";

        return $"{prefix}: {calculation.OrderCount:N0} ilan • {calculation.TotalAmount:N0} adet • Toplam {calculation.TotalPrice:N0} silver";
    }

    private static List<MarketOrderRow> CreateRows(IEnumerable<AuctionEntry> auctionEntries)
    {
        return (auctionEntries ?? [])
            .Where(x => x != null && x.Amount > 0 && x.UnitPriceSilver > 0)
            .GroupBy(x => x.Id)
            .Select(x => new MarketOrderRow(x.Last()))
            .ToList();
    }
}
