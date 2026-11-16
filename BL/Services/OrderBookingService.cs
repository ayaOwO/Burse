using Common.Exceptions;
using Common.Models;
using DAL;

namespace BL.Services;

public class OrderBookingService(IMarketDataProvider provider)
{
    public Trade MatchOrder(Guid traderId, Guid stockId, Order newOrder)
    {
        // go through all opposite orders 
        // 
        //type = bid
        var matchingType = newOrder.Type == OrderType.Bid ? OrderType.Ask : OrderType.Bid;
        var orders = provider.GetOrdersByType(matchingType);
        var possibleOrders = orders
            .Where(order =>
                order.StockId == newOrder.StockId
                && order.Amount <= newOrder.Amount
                && order.Price <= newOrder.Price)
            .OrderBy(order => order.Price)
            .ThenBy(order => order.Amount).ToList();


        foreach (var order in possibleOrders)
        {
            if (newOrder.Amount > order.Amount)
            {
                var traded = int.Min(newOrder.Amount, order.Amount);
                newOrder.Amount -= traded;
                order.Amount = newOrder.Amount;
                if (order.Amount > 0)
                {
                    break;
                }

                provider.CancelOrder(order.Id);
            }

            if (newOrder.Amount > 0)
            {
                provider.AddOrder(newOrder);
            }
        }
    }

    public Order CreateOrder(int traderId, OrderType type, int stockId, double price, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(traderId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);


        var trader = provider.GetTraderById(traderId);
        // Check if the stock exists
        provider.GetStocks(stockId);
        var order = new Order(Guid.CreateVersion7(), type, price, amount, traderId, stockId);
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
