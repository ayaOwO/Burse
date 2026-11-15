using Common;
using System.Text.Json.Serialization;

namespace DAL;

public sealed class MarketDataProviderOptions
{
    [JsonPropertyName("shares")]
    public List<StockInfo> Stocks { get; set; } = [];
    public List<Trader> Traders { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<Trade> Trades { get; set; } = [];
}
