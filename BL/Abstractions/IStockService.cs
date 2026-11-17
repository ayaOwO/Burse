using Common.Models;

namespace BL.Abstractions;

public interface IStockService
{
    public IList<Stock> ListStocks();
    public StockSummary GetStockSummary(int id);
}
