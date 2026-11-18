using BL.Abstractions;
using Common;
using Common.Exceptions;
using Common.Models;
using DAL;
using Microsoft.Extensions.DependencyInjection;

namespace BL.Services;

public class OrderBookingService(
    IMarketDataProvider provider,
    [FromKeyedServices(ServiceKeys.StockSemaphores)]
    IDictionary<int, SemaphoreSlim> stockSemaphoreSlims,
    IOrderMatcher orderMatcher,
    [FromKeyedServices(ServiceKeys.TraderSemaphores)]
    IDictionary<int, SemaphoreSlim> traderSemaphores)
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

    private void ExecuteTrade(Trade trade)
    {
        var cost = trade.Amount * trade.Price;
        // update stock amount
        provider.UpdateTraderMoney(trade.BuyerId, -cost);
        provider.UpdateTraderMoney(trade.SellerId, cost);
        provider.UpdateTraderStocks(trade.SellerId, trade.StockId, -1 * trade.Amount);
        provider.UpdateTraderStocks(trade.BuyerId, trade.StockId, trade.Amount);
        var restingOrder = provider.GetOrderById(trade.OrderIds[0]);
        var initiatingOrder = provider.GetOrderById(trade.OrderIds[1]);
        initiatingOrder.Amount = trade.Amount;
        restingOrder.Amount -= trade.Amount;
        if (initiatingOrder.Amount == 0)
        {
            restingOrder.Status = OrderStatus.Filled;
        }

        if (restingOrder.Amount == 0)
        {
            restingOrder.Status = OrderStatus.Filled;
        }

        provider.AddTrade(trade);
        provider.UpdateStockPrice(trade.StockId, trade.Price);
    }

    public async Task CreateOrder(Order order)
    {
        var stockSemaphore = stockSemaphoreSlims[order.StockId];
        await stockSemaphore.WaitAsync();
        try
        {
            var trades = orderMatcher.MatchOrder(order);
            var traderLockOrder = trades
                .Select(trade => trade.SellerId == order.TraderId ? trade.BuyerId : trade.SellerId)
                .Append(order.TraderId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            var lastLocked = 0;
            try
            {
                foreach (var traderId in traderLockOrder)
                {
                    await traderSemaphores[traderId].WaitAsync();
                    lastLocked++;
                }

                ValidateOrder(order);
                foreach (var trade in trades)
                {
                    ExecuteTrade(trade);
                }

                if (order.Amount == 0)
                {
                    order.Status = OrderStatus.Filled;
                }
                else
                {
                    provider.AddOrder(order);
                }
            }
            finally
            {
                for (var i = lastLocked; i > 0; i--)
                {
                    traderSemaphores[traderLockOrder[i]].Release();
                }
            }
        }
        finally
        {
            stockSemaphore.Release();
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