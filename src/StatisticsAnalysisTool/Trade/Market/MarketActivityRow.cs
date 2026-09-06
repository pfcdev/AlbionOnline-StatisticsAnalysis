using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Enumerations;
using System;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace StatisticsAnalysisTool.Trade.Market;

public sealed class MarketActivityRow
{
    private MarketActivityRow()
    {
    }

    public long TradeId { get; private init; }
    public bool IsPurchase { get; private init; }
    public bool IsSale => !IsPurchase;
    public string ActivityType { get; private init; } = string.Empty;
    public string ItemTypeId { get; private init; } = string.Empty;
    public string ItemName { get; private init; } = string.Empty;
    public string TierAndQuality { get; private init; } = string.Empty;
    public int QualityLevel { get; private init; } = 1;
    public int Quantity { get; private init; }
    public long UnitPrice { get; private init; }
    public long GrossTotal { get; private init; }
    public long Fees { get; private init; }
    public long BalanceChange { get; private init; }
    public long? BalanceAfter { get; private init; }
    public string LocationName { get; private init; } = string.Empty;
    public string ClusterIndex { get; private init; } = string.Empty;
    public DateTime Timestamp { get; private init; }

    public string TimestampDisplay => Timestamp.ToString("g", CultureInfo.CurrentCulture);
    public string UnitPriceDisplay => $"{UnitPrice:N0} silver";
    public string GrossTotalDisplay => $"{GrossTotal:N0} silver";
    public string FeesDisplay => $"{Fees:N0} silver";
    public string BalanceChangeDisplay => $"{(BalanceChange >= 0 ? "+" : string.Empty)}{BalanceChange:N0} silver";
    public string BalanceAfterDisplay => BalanceAfter.HasValue ? $"{BalanceAfter.Value:N0} silver" : "Bu eski işlem için kayıt yok";
    public string DirectionDisplay => IsPurchase ? "Bakiye azaldı" : "Bakiye arttı";
    public BitmapImage Icon => ImageController.GetItemImageWithQuality(ItemTypeId, Math.Max(QualityLevel, 1), 48, 48);

    public static bool TryCreate(Trade trade, out MarketActivityRow row)
    {
        row = null;
        if (trade == null)
        {
            return false;
        }

        var itemTypeId = trade.AuctionEntry?.ItemTypeId
                         ?? trade.MailContent?.UniqueItemName
                         ?? trade.Item?.UniqueName
                         ?? string.Empty;
        var item = ItemController.GetItemByUniqueName(itemTypeId) ?? trade.Item;
        var quality = trade.AuctionEntry?.QualityLevel ?? 0;
        var tierAndQuality = item?.TierLevelString ?? trade.DisplayItemTier ?? string.Empty;
        tierAndQuality = quality > 0
            ? $"{tierAndQuality}  •  Q{quality}".Trim()
            : string.IsNullOrWhiteSpace(tierAndQuality) ? "Kalite bilinmiyor" : $"{tierAndQuality}  •  Kalite bilinmiyor";

        var timestamp = trade.Ticks > DateTime.MinValue.Ticks && trade.Ticks < DateTime.MaxValue.Ticks
            ? new DateTime(trade.Ticks, DateTimeKind.Utc).ToLocalTime()
            : DateTime.MinValue;
        var locationName = string.IsNullOrWhiteSpace(trade.LocationName)
            ? trade.ClusterIndex ?? "Bilinmiyor"
            : trade.LocationName;

        switch (trade.Type)
        {
            case TradeType.InstantBuy:
            case TradeType.InstantSell:
            {
                var content = trade.InstantBuySellContent;
                if (content == null || content.Quantity <= 0)
                {
                    return false;
                }

                var isPurchase = trade.Type == TradeType.InstantBuy;
                var gross = Math.Max(content.UnitPrice.IntegerValue * content.Quantity, 0);
                var distanceFee = Math.Max(content.TotalDistanceFee.IntegerValue, 0);
                var tax = isPurchase ? 0 : Math.Max(content.TaxPrice.IntegerValue, 0);
                var fees = distanceFee + tax;
                row = CreateRow(trade, isPurchase, isPurchase ? "Anında satın alma" : "Anında satış",
                    itemTypeId, item?.LocalizedName, tierAndQuality, quality, content.Quantity,
                    Math.Max(content.UnitPrice.IntegerValue, 0), gross, fees,
                    isPurchase ? -(gross + distanceFee) : gross + distanceFee - tax, locationName, timestamp);
                return true;
            }
            case TradeType.Mail when trade.MailType is MailType.MarketplaceBuyOrderFinished
                                                    or MailType.MarketplaceSellOrderFinished
                                                    or MailType.MarketplaceBuyOrderExpired
                                                    or MailType.MarketplaceSellOrderExpired:
            {
                var content = trade.MailContent;
                var quantity = content?.UsedQuantity ?? 0;
                if (content == null || quantity <= 0)
                {
                    return false;
                }

                var isPurchase = trade.MailType is MailType.MarketplaceBuyOrderFinished or MailType.MarketplaceBuyOrderExpired;
                var isPartial = trade.MailType is MailType.MarketplaceBuyOrderExpired or MailType.MarketplaceSellOrderExpired;
                var gross = Math.Max(content.TotalPrice.IntegerValue, 0);
                var fees = Math.Max(content.TaxPrice.IntegerValue, 0)
                           + Math.Max(content.TaxSetupPrice.IntegerValue, 0)
                           + Math.Max(content.TotalDistanceFee.IntegerValue, 0);
                var unitPrice = Math.Max(content.UnitPriceWithoutTax.IntegerValue, 0);
                var activityType = isPurchase
                    ? isPartial ? "Kısmi Buy Order dolumu" : "Buy Order dolumu"
                    : isPartial ? "Kısmi Sell Order satışı" : "Sell Order satışı";

                row = CreateRow(trade, isPurchase, activityType, itemTypeId, item?.LocalizedName,
                    tierAndQuality, quality, quantity, unitPrice, gross, fees,
                    isPurchase ? -(gross + fees) : gross - fees, locationName, timestamp);
                return true;
            }
            default:
                return false;
        }
    }

    private static MarketActivityRow CreateRow(Trade trade, bool isPurchase, string activityType,
        string itemTypeId, string itemName, string tierAndQuality, int quality, int quantity,
        long unitPrice, long gross, long fees, long balanceChange, string locationName, DateTime timestamp)
    {
        return new MarketActivityRow
        {
            TradeId = trade.Id,
            IsPurchase = isPurchase,
            ActivityType = activityType,
            ItemTypeId = itemTypeId,
            ItemName = string.IsNullOrWhiteSpace(itemName) ? itemTypeId : itemName,
            TierAndQuality = tierAndQuality,
            QualityLevel = quality,
            Quantity = quantity,
            UnitPrice = unitPrice,
            GrossTotal = gross,
            Fees = fees,
            BalanceChange = balanceChange,
            BalanceAfter = trade.BalanceAfter,
            LocationName = locationName,
            ClusterIndex = trade.ClusterIndex ?? string.Empty,
            Timestamp = timestamp
        };
    }
}
