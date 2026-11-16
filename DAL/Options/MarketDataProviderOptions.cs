using Common;
using Microsoft.Extensions.Configuration;

namespace DAL.Options;

public sealed class MarketDataProviderOptions
{
    [ConfigurationKeyName("shares")]
    public List<StockInfo> Stocks { get; set; } = [];
    public List<Trader> Traders { get; set; } = [new Trader(0, "Burse", 0, 0)];
    public List<Order> Orders { get; set; } = [];
    public List<Trade> Trades { get; set; } = [];
}
