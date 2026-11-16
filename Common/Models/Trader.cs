using System.ComponentModel.DataAnnotations;

namespace Common;

public record Trader([Range(0, int.MaxValue)] int Id, string Name, double Money, double InitialMoney = 0)
{
    public IDictionary<int, int> StocksOwned { get; init; } = new Dictionary<int, int>();
}
