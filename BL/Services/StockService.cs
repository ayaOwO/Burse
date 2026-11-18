using BL.Abstractions;
using Common.Models;
using DAL;

namespace BL.Services;

public sealed class StockService(IMarketDataProvider provider) : IStockService
{
    public IList<Stock> ListStocks()
    {
        return provider.GetAllStocks();
    }

    public StockSummary GetStockSummary(int id)
    {
        var stock = provider.GetStocks(id);
        var orders = provider.GetOpenOrdersByStockId(id);
        var trades = provider.GetRecentTradesByStockId(id, 10);
        return new StockSummary(stock, orders, trades);
    }
}
