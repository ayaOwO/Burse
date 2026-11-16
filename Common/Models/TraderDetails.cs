namespace Common.Models;

public sealed record TraderSummary(int Id, string Name, double Money);

public sealed record TraderDetails(IList<Order> Orders, IDictionary<int, int> StocksOwned, double Money);

public sealed record TraderTradesDetails(IList<Trade> Trades, double CurrentBalance, double ProfitLoss);
