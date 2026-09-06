using StatisticsAnalysisTool.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using StatisticsAnalysisTool.Enumerations;

namespace StatisticsAnalysisTool.Trade.Market;

public sealed class MarketActivityBindings : BaseViewModel
{
    private readonly TradeMonitoringBindings _tradeMonitoringBindings;
    private long? _currentSilver;
    private long? _latestTransactionBalance;
    private DateTime _latestTransactionBalanceAtUtc;

    public MarketActivityBindings(TradeMonitoringBindings tradeMonitoringBindings)
    {
        _tradeMonitoringBindings = tradeMonitoringBindings ?? throw new ArgumentNullException(nameof(tradeMonitoringBindings));
        _tradeMonitoringBindings.Trades.CollectionChanged += TradesOnCollectionChanged;
        Rebuild();
    }

    public ObservableCollection<MarketActivityRow> Activities
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public string SearchText
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
            Rebuild();
        }
    } = string.Empty;

    public bool ShowPurchases
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
            Rebuild();
        }
    } = true;

    public bool ShowSales
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
            Rebuild();
        }
    } = true;

    public string CurrentSilverDisplay => _currentSilver.HasValue
        ? $"{_currentSilver.Value:N0} silver"
        : "Oyuna giriş bekleniyor";

    public string BalanceLastUpdatedText
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Bakiye, karakter oyuna girdiğinde canlı güncellenir.";

    public string PurchaseSummary
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "0 işlem";

    public string SalesSummary
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "0 işlem";

    public string ActivitySummary
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Henüz pazar hareketi kaydedilmedi.";

    public void UpdateBalance(long silver, bool associateWithMarketActivity = true)
    {
        _ = RunOnUiThreadAsync(() =>
        {
            _currentSilver = Math.Max(silver, 0);
            OnPropertyChanged(nameof(CurrentSilverDisplay));
            BalanceLastUpdatedText = $"Son bakiye verisi: {DateTime.Now:G}";

            if (associateWithMarketActivity)
            {
                _latestTransactionBalance = _currentSilver;
                _latestTransactionBalanceAtUtc = DateTime.UtcNow;
                AssignBalanceToRecentTrades(_currentSilver.Value, _latestTransactionBalanceAtUtc);
                Rebuild();
            }
        });
    }

    private void TradesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (_latestTransactionBalance.HasValue
            && DateTime.UtcNow - _latestTransactionBalanceAtUtc <= TimeSpan.FromSeconds(8)
            && e.NewItems != null)
        {
            foreach (Trade trade in e.NewItems)
            {
                if (CanReceiveBalanceSnapshot(trade)
                    && IsWithinSnapshotWindow(trade, _latestTransactionBalanceAtUtc))
                {
                    trade.BalanceAfter = _latestTransactionBalance.Value;
                }
            }
        }

        Rebuild();
    }

    private void AssignBalanceToRecentTrades(long balance, DateTime snapshotAtUtc)
    {
        var candidates = (_tradeMonitoringBindings.Trades?.ToArray() ?? [])
            .Where(CanReceiveBalanceSnapshot)
            .Where(trade => trade.BalanceAfter == null && IsWithinSnapshotWindow(trade, snapshotAtUtc))
            .OrderByDescending(trade => trade.Ticks)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var latestTicks = candidates[0].Ticks;
        foreach (var trade in candidates.Where(trade => latestTicks - trade.Ticks <= TimeSpan.FromSeconds(1).Ticks))
        {
            trade.BalanceAfter = balance;
        }
    }

    private static bool CanReceiveBalanceSnapshot(Trade trade)
    {
        if (trade == null)
        {
            return false;
        }

        return trade.Type is TradeType.InstantBuy or TradeType.InstantSell
               || trade.Type == TradeType.Mail
               && trade.MailType is MailType.MarketplaceSellOrderFinished or MailType.MarketplaceSellOrderExpired;
    }

    private static bool IsWithinSnapshotWindow(Trade trade, DateTime snapshotAtUtc)
    {
        if (trade.Ticks <= DateTime.MinValue.Ticks || trade.Ticks >= DateTime.MaxValue.Ticks)
        {
            return false;
        }

        var tradeAtUtc = new DateTime(trade.Ticks, DateTimeKind.Utc);
        var difference = snapshotAtUtc - tradeAtUtc;
        return difference >= TimeSpan.FromSeconds(-2) && difference <= TimeSpan.FromSeconds(8);
    }

    private void Rebuild()
    {
        var trades = _tradeMonitoringBindings.Trades?.ToArray() ?? [];
        var search = SearchText?.Trim() ?? string.Empty;
        var rows = trades
            .Select(trade => MarketActivityRow.TryCreate(trade, out var row) ? row : null)
            .Where(row => row != null)
            .Where(row => (row.IsPurchase && ShowPurchases) || (row.IsSale && ShowSales))
            .Where(row => MatchesSearch(row, search))
            .OrderByDescending(row => row.Timestamp)
            .ThenByDescending(row => row.TradeId)
            .ToList();

        _ = RunOnUiThreadAsync(() =>
        {
            Activities = new ObservableCollection<MarketActivityRow>(rows);
            var purchases = rows.Where(x => x.IsPurchase).ToList();
            var sales = rows.Where(x => x.IsSale).ToList();
            PurchaseSummary = $"{purchases.Count:N0} işlem • {purchases.Sum(x => x.Quantity):N0} adet • {Math.Abs(purchases.Sum(x => x.BalanceChange)):N0} silver gider";
            SalesSummary = $"{sales.Count:N0} işlem • {sales.Sum(x => x.Quantity):N0} adet • {sales.Sum(x => x.BalanceChange):N0} silver net giriş";
            ActivitySummary = rows.Count == 0
                ? "Filtreye uygun pazar hareketi bulunamadı."
                : $"{rows.Count:N0} hareket gösteriliyor • Satıra tıklayarak ayrıntıları açın.";
        });
    }

    private static bool MatchesSearch(MarketActivityRow row, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Contains(row.ItemName, search)
               || Contains(row.ItemTypeId, search)
               || Contains(row.TierAndQuality, search)
               || Contains(row.LocationName, search)
               || Contains(row.ClusterIndex, search)
               || Contains(row.ActivityType, search)
               || row.UnitPrice.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string value, string search)
    {
        return value?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true;
    }
}
