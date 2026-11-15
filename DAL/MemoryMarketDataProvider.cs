using Common;
using Microsoft.Extensions.Options;

namespace DAL;

public class MemoryMarketDataProvider : IMarketDataProvider
{
    private readonly object _stocksLock = new();
    private Dictionary<int, StockInfo> _stocks;
    private Dictionary<int, Trader> _traders;
    private Dictionary<Guid, Order> _orders;
    private Dictionary<int, Trade> _trades;

    public MemoryMarketDataProvider(IOptions<MarketDataProviderOptions> options)
    {

        _traders = new Dictionary<int, Trader>();
        foreach (var trader in options.Value.Traders)
        {
            _traders[trader.Id] = trader with { InitialMoney = trader.Money };
        }

        _orders = new Dictionary<Guid, Order>();
        foreach (var order in options.Value.Orders)
        {
            _orders[order.Id] = order;
        }
        _stocks = new Dictionary<int, StockInfo>();
        foreach (var stock in options.Value.Stocks)
        {
            _stocks[stock.Id] = new StockInfo(stock);

            var order = new Order(Guid.CreateVersion7(), OrderType.Ask, stock.CurrentPrice, stock.Amount, 0, stock.Id);
            _orders.Add(order.Id, order);
        }

        _trades = new Dictionary<int, Trade>();
        foreach (var trade in options.Value.Trades)
        {
            _trades[trade.Id] = trade;
        }
    }

    public IList<StockInfo> GetAllStocks()
    {
        lock (_stocksLock)
        {
            return _stocks.Values.Select(stock => new StockInfo(stock)).ToList();
        }
    }

    public StockInfo GetStocks(int id)
    {
        lock (_stocksLock)
        {
            return new StockInfo(_stocks[id]);
        }
    }

    public void UpdateStock(StockInfo stock)
    {
        lock (_stocksLock)
        {
            _stocks[stock.Id] = new StockInfo(stock);
        }
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
