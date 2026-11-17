using BL.Abstractions;
using Common.Models;
using DAL;

namespace BL.Services;

public sealed class TraderService(IMarketDataProvider provider) : ITraderService
{
    public IList<TraderSummary> GetTraders() => provider.GetAllTraders()
        .Select(trader => new TraderSummary(trader.Id, trader.Name, trader.Money)).ToList();

    public TraderDetails GetTrader(int id)
    {
        var trader = provider.GetTraderById(id);
        var orders = provider.GetOrdersByTraderId(id);
        return new TraderDetails(orders, trader.StocksOwned, trader.Money);
    }

    public TraderTradesDetails GetTrades(int id)
    {
        var trader = provider.GetTraderById(id);
        var trades = provider.GetTradesByTraderId(id).OrderByDescending(trade => trade.Id).Take(8).ToList();
        var holdingsValue = provider.GetTraderHoldingsValue(id);
        return new TraderTradesDetails(trades, trader.Money,
            trader.Money + holdingsValue - trader.InitialPortfolioValue);
    }
}
