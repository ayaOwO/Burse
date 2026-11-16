using Common;
using DAL;

namespace BL.Services;

public sealed class OrderService(IMarketDataProvider provider)
{
    public Order CreateOrder(int traderId, OrderType type, int stockId, double price, int amount)
    {
        if (price <= 0)
        {
            throw new ArgumentException("Price must be positive and finite.");
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be positive.");
        }

        provider.GetTraderById(traderId);
        provider.GetStocks(stockId);
        var order = new Order(Guid.CreateVersion7(), type, price, amount, traderId, stockId);
        provider.AddOrder(order);
        return order;
    }

    public void CancelOrder(Guid id)
    {
        provider.CancelOrder(id);
    }
}