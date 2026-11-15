using Common;

namespace DAL;

public interface IMarketDataProvider
{
    public IList<StockInfo> GetAllStocks();
    public StockInfo GetStocks(int id);
    public IList<Order> GetOrdersByStockId(int stockId);
    public IList<Trade> GetTradesByStockId(int stockId);
    public IList<Trade> GetTradesByTraderId(int traderId);
    public Trader GetTraderById(int id);
    public IList<Trader> GetAllTraders();
}
