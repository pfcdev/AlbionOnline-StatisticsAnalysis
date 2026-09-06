using StatisticsAnalysisTool.Common;
using System;
using System.Windows.Media.Imaging;

namespace StatisticsAnalysisTool.Trade.Market;

public sealed class MarketOrderRow
{
    private readonly string _fallbackItemName;

    public MarketOrderRow(AuctionEntry auctionEntry)
    {
        ArgumentNullException.ThrowIfNull(auctionEntry);

        Id = auctionEntry.Id;
        ItemTypeId = auctionEntry.ItemTypeId ?? string.Empty;
        QualityLevel = auctionEntry.QualityLevel;
        Amount = Math.Max(auctionEntry.Amount, 0);
        UnitPrice = (ulong) Math.Max(FixPoint.FromInternalValue(auctionEntry.UnitPriceSilver).IntegerValue, 0);
        Expires = auctionEntry.Expires;
        SellerName = auctionEntry.SellerName ?? string.Empty;

        var item = ItemController.GetItemByUniqueName(ItemTypeId);
        _fallbackItemName = item?.LocalizedName ?? ItemTypeId;
        TierAndQuality = item == null
            ? $"Q{QualityLevel}"
            : $"{item.TierLevelString}  •  Q{QualityLevel}";
    }

    public long Id { get; }

    public string ItemTypeId { get; }

    public string ItemName => _fallbackItemName;

    public string TierAndQuality { get; }

    public int QualityLevel { get; }

    public int Amount { get; }

    public ulong UnitPrice { get; }

    public decimal TotalPrice => UnitPrice * (decimal) Amount;

    public DateTime Expires { get; }

    public string SellerName { get; }

    public string ExpiresDisplay => Expires == default ? "-" : Expires.ToLocalTime().ToString("g");

    public BitmapImage Icon => ImageController.GetItemImageWithQuality(ItemTypeId, QualityLevel, 48, 48);
}
