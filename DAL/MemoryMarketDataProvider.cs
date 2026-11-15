using Common;
using Microsoft.Extensions.Options;

namespace DAL;

public class MemoryMarketDataProvider : IMarketDataProvider
{
    private Dictionary<int, StockInfo> _stocks;
    private Dictionary<int, Trader> _traders;
    private Dictionary<int, Order> _orders;
    private Dictionary<int, Trade> _trades;

    public MemoryMarketDataProvider(IOptions<MarketDataProviderOptions> options)
    {
        _stocks = new Dictionary<int, StockInfo>();
        foreach (var stock in options.Value.Stocks)
        {
            _stocks[stock.Id] = stock;
        }

        _traders = new Dictionary<int, Trader>();
        foreach (var trader in options.Value.Traders)
        {
            _traders[trader.Id] = trader;
        }

        _orders = new Dictionary<int, Order>();
        foreach (var order in options.Value.Orders)
        {
            _orders[order.Id] = order;
        }

        _trades = new Dictionary<int, Trade>();
        foreach (var trade in options.Value.Trades)
        {
            _trades[trade.Id] = trade;
        }
    }

    public IList<StockInfo> GetAllStocks()
    {
        return [.. _stocks.Values];
    }

    public StockInfo GetStocks(int id)
    {
        return _stocks[id];
    }

    public IList<Order> GetOrdersByStockId(int stockId)
    {
        return _orders.Values.Where(order => order.StockId == stockId).ToList();
    }

    public IList<Trade> GetTradesByStockId(int stockId)
    {
        return _trades.Values.Where(trade => trade.OrderIds.Any(orderId =>
            _orders.TryGetValue(orderId, out var order) && order.StockId == stockId)).ToList();
    }

    public IList<Trade> GetTradesByTraderId(int traderId)
    {
        return _trades.Values.Where(trade => trade.OrderIds.Any(orderId =>
            _orders.TryGetValue(orderId, out var order) && order.OwnerId == traderId)).ToList();
    }

    public Trader GetTraderById(int id)
    {
        return _traders[id];
    }

    public IList<Trader> GetAllTraders()
    {
        return _traders.Values.ToList();
    }
}
