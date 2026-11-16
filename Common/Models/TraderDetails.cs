using System.ComponentModel.DataAnnotations;

namespace Common.Models;

public sealed record TraderSummary([Range(0, int.MaxValue)] int Id, string Name, double Money);

public sealed record TraderDetails(IList<Order> Orders, IDictionary<int, int> StocksOwned, double Money);

public sealed record TraderTradesDetails(IList<Trade> Trades, double CurrentBalance, double ProfitLoss);
