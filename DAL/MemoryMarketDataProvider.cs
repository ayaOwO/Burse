using Common;
using DAL.Options;
using Microsoft.Extensions.Options;

namespace DAL;

public class MemoryMarketDataProvider : IMarketDataProvider
{
    private readonly object _stocksLock = new();
    private readonly Lock _ordersLock = new();
    private Dictionary<int, StockInfo> _stocks;
    private Dictionary<int, Trader> _traders;
    private Dictionary<Guid, Order> _orders;
    private Dictionary<int, Trade> _trades;

    public MemoryMarketDataProvider(IOptions<MarketDataProviderOptions> options)
    {

        _traders = new Dictionary<int, Trader>();
        foreach (var trader in options.Value.Traders)
        {
            _traders[trader.Id] = trader with
            {
                InitialMoney = trader.Money,
                StocksOwned = new Dictionary<int, int>(trader.StocksOwned)
            };
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
        lock (_ordersLock)
        {
            return _orders.Values.Where(order => order.StockId == stockId).ToList();
        }
    }

    public IList<Order> GetOrdersByTraderId(int traderId)
    {
        lock (_ordersLock)
        {
            return _orders.Values.Where(order => order.OwnerId == traderId).ToList();
        }
    }

    public void AddOrder(Order order)
    {
        lock (_ordersLock)
        {
            _orders.Add(order.Id, order);
        }
    }

    public void CancelOrder(Guid id)
    {
        lock (_ordersLock)
        {
            var order = _orders[id];
            if (order.Status == OrderStatus.Filled)
            {
                throw new InvalidOperationException("A filled order cannot be cancelled.");
            }

            _orders[id] = order with { Status = OrderStatus.Cancelled };
        }
    }

    public IList<Trade> GetTradesByStockId(int stockId)
    {
        lock (_ordersLock)
        {
            return _trades.Values.Where(trade => trade.OrderIds.Any(orderId =>
                _orders.TryGetValue(orderId, out var order) && order.StockId == stockId)).ToList();
        }
    }

    public IList<Trade> GetTradesByTraderId(int traderId)
    {
        lock (_ordersLock)
        {
            return _trades.Values.Where(trade => trade.OrderIds.Any(orderId =>
                _orders.TryGetValue(orderId, out var order) && order.OwnerId == traderId)).ToList();
        }
    }

    public Trader GetTraderById(int id)
    {
        var trader = _traders[id];
        return trader with { StocksOwned = new Dictionary<int, int>(trader.StocksOwned) };
    }

    public IList<Trader> GetAllTraders()
    {
        return _traders.Values.Select(trader => trader with
        {
            StocksOwned = new Dictionary<int, int>(trader.StocksOwned)
        }).ToList();
    }
}
