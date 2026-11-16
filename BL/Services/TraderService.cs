using Common.Models;
using DAL;

namespace BL.Services;

public sealed class TraderService(IMarketDataProvider provider)
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
        return new TraderTradesDetails(provider.GetTradesByTraderId(id), trader.Money,
            trader.Money - trader.InitialMoney);
    }
}