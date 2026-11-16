using Common;
using Common.Exceptions;
using Common.Models;
using DAL;

namespace BL.Services;

public sealed class OrderService(IMarketDataProvider provider)
{
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