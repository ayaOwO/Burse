using Common.Exceptions;
using Common.Models;
using DAL;

namespace BL.Services;

public class OrderBookingService(IMarketDataProvider provider)
{
    public Order MatchOrder(Guid traderId, Guid stockId, Order newOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newOrder.Amount);
        var matchingType = newOrder.Type == OrderType.Bid ? OrderType.Ask : OrderType.Bid;
        var orders = provider.GetOrdersByType(matchingType);
        var possibleOrders = orders
            .Where(order =>
                order.StockId == newOrder.StockId
                && order is { Status: OrderStatus.Open, Amount: > 0 }
                && (newOrder.Type == OrderType.Bid
                    ? order.Price <= newOrder.Price
                    : order.Price >= newOrder.Price));
        var prioritizedOrders = newOrder.Type == OrderType.Bid
            ? possibleOrders.OrderBy(order => order.Price)
            : possibleOrders.OrderByDescending(order => order.Price);
        var matched = new List<Order>();
        foreach (var order in prioritizedOrders.ThenBy(order => order.Id))
        {
            var traded = Math.Min(newOrder.Amount, order.Amount);
            order.Amount -= traded;
            newOrder.Amount -= traded;
            if (order.Amount == 0)
            {
                order.Status = OrderStatus.Filled;
            }

            matched.Add(order);
            if (newOrder.Amount == 0)
            {
                break;
            }
        }

        if (newOrder.Amount == 0)
            newOrder.Status = OrderStatus.Filled;
        return newOrder;
    }

    public Order CreateOrder(int traderId, OrderType type, int stockId, double price, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(traderId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);


        var trader = provider.GetTraderById(traderId);
        // Check if the stock exists
        provider.GetStocks(stockId);
        var order = new Order
        {
            Id = Guid.CreateVersion7(), Type = type, Price = price, Amount = amount,
            OwnerId = traderId, StockId = stockId
        };
        switch (type)
        {
            case OrderType.Ask when !trader.StocksOwned.TryGetValue(stockId, out var owned) || owned < amount:
                throw new InsufficientStocksException();
            case OrderType.Bid when price * amount > trader.Money:
                throw new InsufficientFundsException();
        }

        provider.AddOrder(order);
        return order;
    }

    public void CancelOrder(Guid id)
    {
        provider.CancelOrder(id);
    }
}