using BL.Abstractions;
using Common.Models;
using DAL;

namespace BL.Services;

public class OrderMatcher(IMarketDataProvider provider) : IOrderMatcher
{
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

    public List<Trade> MatchOrder(Order newOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newOrder.Amount);
        var trades = new List<Trade>();
        var prioritizedOrders = GetMatchingOrders(newOrder);
        var orderFilledAmount = 0;
        foreach (var order in prioritizedOrders)
        {
            var amountTraded = Math.Min(newOrder.Amount, order.Amount);
            orderFilledAmount += amountTraded;
            trades.Add(new Trade
            {
                OrderIds = [order.Id, newOrder.Id],
                StockId = newOrder.StockId,
                BuyerId = newOrder.Type == OrderType.Bid ? newOrder.TraderId : order.TraderId,
                SellerId = newOrder.Type == OrderType.Ask ? newOrder.TraderId : order.TraderId,
                Amount = amountTraded,
                Price = order.Price
            });
            if (newOrder.Amount == 0)
            {
                break;
            }
        }

        return trades;
    }
}