using Common;

namespace DAL;

public sealed class MarketDataProviderOptions
{
    public List<StockInfo> Stocks { get; set; } = [];
    public List<Trader> Traders { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<Trade> Trades { get; set; } = [];
}
