using BL.Abstractions;
using Common.Exceptions;
using Common.Models;
using DAL;

namespace BL.Services;

public class OrderBookingService(IMarketDataProvider provider, IDictionary<int, SemaphoreSlim> stockSemaphoreSlims,
    IOrderMatcher orderMatcher)
    : IOrderBookingService
{
    private void ValidateOrder(Order order)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order.Amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order.TraderId);

        var trader = provider.GetTraderById(order.TraderId);
        // Ensure stock exists
        var unused = provider.GetStocks(order.StockId);
        var openOrders = provider.GetOrdersByTraderId(order.TraderId)
            .Where(o => o.Status == OrderStatus.Open).ToList();
        if (openOrders.Any(o => o.StockId == order.StockId && o.Type != order.Type))
            throw new InvalidOperationException("Cannot hold buy and sell offers for the same stock.");
        if (order.Type == OrderType.Ask)
        {
            trader.StocksOwned.TryGetValue(order.StockId, out var owned);
            var listed = provider.GetStockCountOfOpenOrdersByTraders(order.TraderId, order.StockId);
            if (order.Amount > owned - listed)
                throw new InsufficientStocksException();
        }
        else
        {
            var committed = provider.GetCommittedMoney(order.TraderId);
            if (order.Price * order.Amount > trader.Money - committed)
                throw new InsufficientFundsException();
        }
    }

    public Order CreateOrder(Order order)
    {
        stockSemaphoreSlims[order.StockId].Wait();
        try
        {
            ValidateOrder(order);
            var trades = orderMatcher.MatchOrder(order);
            foreach (var trade in trades)
            {
                var cost = trade.Amount * trade.Price;
                provider.UpdateTraderMoney(trade.BuyerId, -cost);
                provider.UpdateTraderMoney(trade.SellerId, cost);
                provider.UpdateTraderStocks(trade.SellerId, trade.StockId, -1 * trade.Amount);
                provider.UpdateTraderStocks(trade.BuyerId, trade.StockId, trade.Amount);
                provider.AddTrade(trade);
                provider.UpdateStockPrice(trade.StockId, trade.Price);
            }

            provider.AddOrder(order);
            return order;
        }
        finally
        {
            stockSemaphoreSlims[order.StockId].Release();
        }
    }

    public void CancelOrder(Guid id)
    {
        var stockId = provider.GetOrderById(id).StockId;
        stockSemaphoreSlims[stockId].Wait();
        try
        {
            provider.CancelOrder(id);
        }
        finally
        {
            stockSemaphoreSlims[stockId].Release();
        }
    }
}
