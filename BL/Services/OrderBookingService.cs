using Common.Exceptions;
using Common.Models;
using DAL;

namespace BL.Services;

public class OrderBookingService(IMarketDataProvider provider)
{
    // shortcut: orders are serialized within one process; use database transactions when running multiple instances.
    private static readonly SemaphoreSlim OrderSemaphore = new(1, 1);

    public IList<Trade> MatchOrder(Order newOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newOrder.Amount);
        var trades = new List<Trade>();
        foreach (var order in provider.GetMatchingOrders(newOrder))
        {
            var traded = Math.Min(newOrder.Amount, order.Amount);
            order.Amount -= traded;
            newOrder.Amount -= traded;
            trades.Add(new Trade
            {
                OrderIds = [order.Id, newOrder.Id],
                StockId = newOrder.StockId,
                BuyerId = newOrder.Type == OrderType.Bid ? newOrder.TraderId : order.TraderId,
                SellerId = newOrder.Type == OrderType.Ask ? newOrder.TraderId : order.TraderId,
                Amount = traded,
                Price = order.Price
            });
            if (order.Amount == 0)
            {
                order.Status = OrderStatus.Filled;
            }

            if (newOrder.Amount == 0)
            {
                break;
            }
        }

        if (newOrder.Amount == 0)
            newOrder.Status = OrderStatus.Filled;
        return trades;
    }

    public Order CreateOrder(int traderId, OrderType type, int stockId, double price, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(traderId);
        if (!double.IsFinite(price) || price <= 0 || !double.IsFinite(price * amount))
            throw new ArgumentOutOfRangeException(nameof(price));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        OrderSemaphore.Wait();
        try
        {
            var trader = provider.GetTraderById(traderId);
            var stock = provider.GetStocks(stockId);
            var openOrders = provider.GetOrdersByTraderId(traderId)
                .Where(order => order.Status == OrderStatus.Open).ToList();
            if (openOrders.Any(order => order.StockId == stockId && order.Type != type))
                throw new InvalidOperationException("Cannot hold buy and sell offers for the same stock.");
            if (type == OrderType.Ask)
            {
                trader.StocksOwned.TryGetValue(stockId, out var owned);
                var listed = provider.GetListedStockCount(traderId, stockId);
                if (amount > owned - listed)
                    throw new InsufficientStocksException();
            }
            else
            {
                var committed = provider.GetCommittedMoney(traderId);
                if (price * amount > trader.Money - committed)
                    throw new InsufficientFundsException();
            }

            var order = new Order
            {
                Id = Guid.CreateVersion7(), Type = type, Price = price, Amount = amount,
                TraderId = traderId, StockId = stockId
            };
            var trades = MatchOrder(order);
            foreach (var trade in trades)
            {
                var cost = trade.Amount * trade.Price;
                provider.UpdateTraderMoney(trade.BuyerId, -cost);
                provider.UpdateTraderMoney(trade.SellerId, cost);
                provider.UpdateTraderStocks(trade.SellerId, trade.StockId, -trade.Amount);
                provider.UpdateTraderStocks(trade.BuyerId, trade.StockId, trade.Amount);
                provider.AddTrade(trade);
                stock.CurrentPrice = trade.Price;
                provider.UpdateStock(stock);
            }
            provider.AddOrder(order);
            return order;
        }
        finally
        {
            OrderSemaphore.Release();
        }
    }

    public void CancelOrder(Guid id)
    {
        OrderSemaphore.Wait();
        try
        {
            provider.CancelOrder(id);
        }
        finally
        {
            OrderSemaphore.Release();
        }
    }
}
