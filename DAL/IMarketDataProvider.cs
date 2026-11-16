using Common;
using Common.Models;

namespace DAL;

public interface IMarketDataProvider
{
    public IList<StockInfo> GetAllStocks();
    public StockInfo GetStocks(int id);
    public void UpdateStock(StockInfo stock);
    public IList<Order> GetOrdersByStockId(int stockId);
    public IList<Order> GetOrdersByType(OrderType type);
    public IList<Order> GetOrdersByTraderId(int traderId);
    public void AddOrder(Order order);
    public void CancelOrder(Guid id);
    public IList<Trade> GetTradesByStockId(int stockId);
    public IList<Trade> GetTradesByTraderId(int traderId);
    public Trader GetTraderById(int id);
    public IList<Trader> GetAllTraders();
}