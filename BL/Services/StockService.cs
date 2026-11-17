using Common.Models;
using DAL;

namespace BL.Services;

public sealed class StockService(IMarketDataProvider provider)
{
    public IList<StockInfo> GetStocks()
    {
        return provider.GetAllStocks();
    }

    public StockDetails GetStock(int id)
    {
        var stock = provider.GetStocks(id);
        var orders = provider.GetOrdersByStockId(id)
            .Select(order => new StockOrder(order.Id, order.TraderId, order.Type, order.Price, order.Amount, order.Status))
            .ToList();
        var trades = provider.GetRecentTradesByStockId(id, 10);
        return new StockDetails(stock, orders, trades);
    }
}
