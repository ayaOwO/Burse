using Common.Models;

namespace DAL;

public interface IMarketDataProvider
{
    public IList<StockInfo> GetAllStocks();
    public StockInfo GetStocks(int stockId);
    public void UpdateStock(StockInfo stock);
    public IList<Order> GetOrdersByStockId(int stockId);
    public IList<Order> GetOrdersByType(OrderType type);
    public IList<Order> GetOrdersByTraderId(int traderId);
    public IList<Order> GetMatchingOrders(Order order);
    public long GetListedStockCount(int traderId, int stockId);
    public double GetCommittedMoney(int traderId);
    public void AddOrder(Order order);
    public void AddTrade(Trade trade);
    public void CancelOrder(Guid id);
    public IList<Trade> GetTradesByStockId(int stockId);
    public IList<Trade> GetRecentTradesByStockId(int stockId, int count);
    public IList<Trade> GetTradesByTraderId(int traderId);
    public Trader GetTraderById(int id);
    public double GetTraderHoldingsValue(int traderId);
    public void UpdateTraderStocks(int traderId, int stockId, int count);
    public void UpdateTraderMoney(int traderId, double amount);
    public IList<Trader> GetAllTraders();
}
