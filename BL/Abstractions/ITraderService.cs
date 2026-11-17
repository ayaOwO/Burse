using Common.Models;

namespace BL.Abstractions;

public interface ITraderService
{
    public IList<TraderSummary> GetTraders();
    public TraderDetails GetTrader(int id);
    public TraderTradesDetails GetTrades(int id);
}
