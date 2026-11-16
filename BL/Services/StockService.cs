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
            .Select(order => new StockOrder(order.Id, order.OwnerId, order.Type, order.Price, order.Amount, order.Status))
            .ToList();
        // shortcut: trade IDs determine recency; use timestamps when matching records execution times.
        var trades = provider.GetTradesByStockId(id).OrderByDescending(trade => trade.Id).Take(10).ToList();
        return new StockDetails(stock, orders, trades);
    }
}
