using BL.Abstractions;
using Common.Exceptions;
using Common.Models;
using DAL;

namespace BL.Services;

public class OrderBookingService(IMarketDataProvider provider) : IOrderBookingService
{
    private static readonly SemaphoreSlim OrderSemaphore = new(1, 1);

    private IList<Order> GetMatchingOrders(Order newOrder)
    {
        var matchingType = newOrder.Type == OrderType.Bid ? OrderType.Ask : OrderType.Bid;
        var relevantOrders = provider.GetOrdersByStockId(newOrder.StockId).Where(order =>
            order.Type == matchingType && order is { Status: OrderStatus.Open, Amount: > 0 } &&
            (newOrder.Type == OrderType.Bid ? order.Price <= newOrder.Price : order.Price >= newOrder.Price));
        var prioritizedOrders = newOrder.Type == OrderType.Bid
            ? relevantOrders.OrderBy(order => order.Price)
            : relevantOrders.OrderByDescending(order => order.Price);
        // guid v7 is sortable by timestamp
        return prioritizedOrders.ThenBy(order => order.Id).ToList();
    }

    private IList<Trade> MatchOrder(Order newOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newOrder.Amount);
        var trades = new List<Trade>();
        var prioritizedOrders = GetMatchingOrders(newOrder);
        foreach (var order in prioritizedOrders)
        {
            var amountTraded = Math.Min(newOrder.Amount, order.Amount);
            order.Amount -= amountTraded;
            newOrder.Amount -= amountTraded;
            trades.Add(new Trade
            {
                OrderIds = [order.Id, newOrder.Id],
                StockId = newOrder.StockId,
                BuyerId = newOrder.Type == OrderType.Bid ? newOrder.TraderId : order.TraderId,
                SellerId = newOrder.Type == OrderType.Ask ? newOrder.TraderId : order.TraderId,
                Amount = amountTraded,
                Price = order.Price
            });
            if (order.Amount == 0)
            {
                order.Status = OrderStatus.Filled;
            }

            if (newOrder.Amount == 0)
            {
                newOrder.Status = OrderStatus.Filled;
                break;
            }
        }

        return trades;
    }

    private void ValidateOrder(Order order)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order.Amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order.TraderId);

        var trader = provider.GetTraderById(order.TraderId);
        // Ensure stock exists
        var unused = provider.GetStocks(order.StockId);
        var openOrders = provider.GetOrdersByTraderId(order.TraderId)
            .Where(o => o.Status == OrderStatus.Open).ToList();
        if (openOrders.Any(o => o.StockId == order.StockId && o.Type != order.Type))
            throw new InvalidOperationException("Cannot hold buy and sell offers for the same stock.");
        if (order.Type == OrderType.Ask)
        {
            trader.StocksOwned.TryGetValue(order.StockId, out var owned);
            var listed = provider.GetStockCountOfOpenOrdersByTraders(order.TraderId, order.StockId);
            if (order.Amount > owned - listed)
                throw new InsufficientStocksException();
        }
        else
        {
            var committed = provider.GetCommittedMoney(order.TraderId);
            if (order.Price * order.Amount > trader.Money - committed)
                throw new InsufficientFundsException();
        }
    }

    public Order CreateOrder(Order order)
    {
        OrderSemaphore.Wait();
        try
        {
            ValidateOrder(order);
            var trades = MatchOrder(order);
            foreach (var trade in trades)
            {
                var cost = trade.Amount * trade.Price;
                provider.UpdateTraderMoney(trade.BuyerId, -cost);
                provider.UpdateTraderMoney(trade.SellerId, cost);
                provider.UpdateTraderStocks(trade.SellerId, trade.StockId, -1 * trade.Amount);
                provider.UpdateTraderStocks(trade.BuyerId, trade.StockId, trade.Amount);
                provider.AddTrade(trade);
                provider.UpdateStockPrice(trade.StockId, trade.Price);
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