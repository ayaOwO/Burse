using Common.Models;
using DAL;

namespace BL.Services;

public sealed class StockService(IMarketDataProvider provider)
{
    public IList<Stock> ListStocks()
    {
        return provider.GetAllStocks();
    }

    public StockSummary GetStockSummary(int id)
    {
        var stock = provider.GetStocks(id);
        var orders = provider.GetOrdersByStockId(id)
            .Select(order => new StockOrder(order.Id, order.TraderId, order.Type, order.Price, order.Amount, order.Status))
            .ToList();
        var trades = provider.GetRecentTradesByStockId(id, 10);
        return new StockSummary(stock, orders, trades);
    }
}
