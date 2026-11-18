using BL.Abstractions;
using Common.Models;
using DAL;
using Microsoft.Extensions.Logging;

namespace BL.Services;

public class OrderMatcher(IMarketDataProvider provider, ILogger<OrderMatcher> logger) : IOrderMatcher
{
    private List<Order> GetMatchingOrders(Order newOrder)
    {
        logger.LogDebug("Getting matching orders for order {@Order}", newOrder);
        var matchingType = newOrder.Type == OrderType.Bid ? OrderType.Ask : OrderType.Bid;
        var relevantOrders = provider.GetOpenOrdersByStockId(newOrder.StockId).Where(order =>
            order.Type == matchingType && order.Amount > 0 &&
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
            var amountTraded = Math.Min(newOrder.Amount - orderFilledAmount, order.Amount);
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
            logger.LogTrace("Found a new valid trade {@Trade}", trades.Last());
            if (orderFilledAmount == newOrder.Amount)
            {
                break;
            }
        }

        logger.LogDebug("Found {TradeCounts} trades for order {@Order} with {OrderAmountLeft}", trades.Count, newOrder,
            newOrder.Amount - orderFilledAmount);
        return trades;
    }
}