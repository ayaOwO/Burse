using Common.Models;
using DAL.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DAL;

public class MemoryMarketDataProvider : IMarketDataProvider
{
    private readonly Lock _stocksLock = new();
    private readonly Lock _ordersLock = new();
    private readonly Lock _tradersLock = new();

    private readonly ILogger<MemoryMarketDataProvider> _logger;

    private readonly Dictionary<int, Stock> _stocks;
    private readonly Dictionary<int, Trader> _traders;
    private readonly Dictionary<Guid, Order> _activeOrders;
    private readonly Dictionary<int, Trade> _trades;

    public MemoryMarketDataProvider(IOptions<MarketDataProviderOptions> options,
        ILogger<MemoryMarketDataProvider> logger)
    {
        _logger = logger;
        _traders = new Dictionary<int, Trader>();
        foreach (var trader in options.Value.Traders)
        {
            _traders[trader.Id] = trader with
            {
                InitialMoney = trader.Money,
                StocksOwned = new Dictionary<int, int>(trader.StocksOwned)
            };
        }

        _activeOrders = new Dictionary<Guid, Order>();
        foreach (var order in options.Value.Orders)
        {
            _activeOrders[order.Id] = order;
        }

        _stocks = new Dictionary<int, Stock>();
        foreach (var stock in options.Value.Stocks)
        {
            _stocks[stock.Id] = new Stock(stock);
            _traders[0].StocksOwned[stock.Id] = stock.Amount;

            var order = new Order
            {
                Id = Guid.CreateVersion7(),
                Type = OrderType.Ask,
                Price = stock.CurrentPrice,
                Amount = stock.Amount,
                TraderId = 0,
                StockId = stock.Id
            };
            _activeOrders.Add(order.Id, order);
        }

        foreach (var trader in _traders.Values.ToList())
        {
            _traders[trader.Id] = trader with
            {
                InitialPortfolioValue = trader.Money + _stocks.Values.Sum(stock =>
                    trader.StocksOwned.TryGetValue(stock.Id, out var count) ? count * stock.CurrentPrice : 0)
            };
        }

        _trades = new Dictionary<int, Trade>();
        foreach (var trade in options.Value.Trades)
        {
            _trades[trade.Id] = trade;
        }
    }

    public IList<Stock> GetAllStocks()
    {
        lock (_stocksLock)
        {
            return _stocks.Values.Select(stock => new Stock(stock)).ToList();
        }
    }

    public Stock GetStocks(int stockId)
    {
        lock (_stocksLock)
        {
            return new Stock(_stocks[stockId]);
        }
    }

    public void UpdateStockPrice(int stockId, double price)
    {
        lock (_stocksLock)
        {
            _stocks[stockId].CurrentPrice = price;
        }
    }

    public IList<Order> GetOrdersByStockId(int stockId)
    {
        lock (_ordersLock)
        {
            return _activeOrders.Values.Where(order => order.StockId == stockId).ToList();
        }
    }

    public IList<Order> GetOrdersByType(OrderType type)
    {
        lock (_ordersLock)
        {
            return _activeOrders.Values.Where(order => order.Type == type).ToList();
        }
    }

    public IList<Order> GetOrdersByTraderId(int traderId)
    {
        lock (_ordersLock)
        {
            return _activeOrders.Values.Where(order => order.TraderId == traderId).ToList();
        }
    }


    public long GetStockCountOfOpenOrdersByTraders(int traderId, int stockId)
    {
        lock (_ordersLock)
        {
            return _activeOrders.Values.Where(order => order.TraderId == traderId
                                                       && order.StockId == stockId && order is { Type: OrderType.Ask, Status: OrderStatus.Open })
                .Sum(order => (long)order.Amount);
        }
    }

    public double GetCommittedMoney(int traderId)
    {
        lock (_ordersLock)
        {
            return _activeOrders.Values.Where(order => order.TraderId == traderId
                                                       && order.Type == OrderType.Bid &&
                                                       order.Status == OrderStatus.Open)
                .Sum(order => order.Price * order.Amount);
        }
    }

    public void AddOrder(Order order)
    {
        lock (_ordersLock)
        {
            _activeOrders.Add(order.Id, order);
        }
    }

    public void AddTrade(Trade trade)
    {
        lock (_ordersLock)
        {
            var id = checked(_trades.Keys.DefaultIfEmpty(0).Max() + 1);
            _trades.Add(id, trade with { Id = id });
        }
    }

    public Order GetOrderById(Guid id)
    {
        lock (_ordersLock)
        {
            return _activeOrders[id];
        }
    }

    public void CancelOrder(Guid id)
    {
        lock (_ordersLock)
        {
            var order = _activeOrders[id];
            if (order.Status == OrderStatus.Filled)
            {
                throw new InvalidOperationException("Filled orders cannot be cancelled.");
            }

            order.Status = OrderStatus.Cancelled;
        }
    }

    public IList<Trade> GetTradesByStockId(int stockId)
    {
        lock (_ordersLock)
        {
            return _trades.Values.Where(trade => trade.OrderIds.Any(orderId =>
                _activeOrders.TryGetValue(orderId, out var order) && order.StockId == stockId)).ToList();
        }
    }

    public IList<Trade> GetRecentTradesByStockId(int stockId, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        // GUID v7 is sortable by time
        return GetTradesByStockId(stockId).OrderByDescending(trade => trade.Id).Take(count).ToList();
    }

    public IList<Trade> GetTradesByTraderId(int traderId)
    {
        lock (_ordersLock)
        {
            return _trades.Values.Where(trade => trade.OrderIds.Any(orderId =>
                _activeOrders.TryGetValue(orderId, out var order) && order.TraderId == traderId)).ToList();
        }
    }

    public Trader GetTraderById(int id)
    {
        lock (_tradersLock)
        {
            var trader = _traders[id];
            return trader with { StocksOwned = new Dictionary<int, int>(trader.StocksOwned) };
        }
    }

    public double GetTraderHoldingsValue(int traderId)
    {
        lock (_tradersLock)
        lock (_stocksLock)
        {
            return _traders[traderId].StocksOwned.Sum(item =>
            {
                var (stockId, count) = item;
                return _stocks[stockId].CurrentPrice * count;
            });
        }
    }

    public void UpdateTraderStocks(int traderId, int stockId, int count)
    {
        lock (_tradersLock)
        {
            var trader = _traders[traderId];
            var stocks = trader.StocksOwned;
            stocks.TryGetValue(stockId, out var owned);
            var updatedCount = checked(owned + count);
            if (updatedCount < 0)
            {
                _logger.LogWarning(
                    "Trader {TraderId} stock {StockId} count went below zero ({Count}); removing holding.",
                    traderId, stockId, updatedCount);
                stocks.Remove(stockId);
            }
            else
            {
                stocks[stockId] = updatedCount;
            }
        }
    }

    public void UpdateTraderMoney(int traderId, double amount)
    {
        if (!double.IsFinite(amount))
            throw new ArgumentOutOfRangeException(nameof(amount));
        lock (_tradersLock)
        {
            var trader = _traders[traderId];
            var money = trader.Money + amount;
            if (!double.IsFinite(money) || money < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            _traders[traderId] = trader with { Money = money };
        }
    }

    public IList<Trader> GetAllTraders()
    {
        lock (_tradersLock)
        {
            return _traders.Values.Select(trader => trader with
            {
                StocksOwned = new Dictionary<int, int>(trader.StocksOwned)
            }).ToList();
        }
    }
}