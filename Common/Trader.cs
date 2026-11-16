namespace Common;

public record Trader(int Id, string Name, double Money, double InitialMoney = 0)
{
    public IDictionary<int, int> StocksOwned { get; init; } = new Dictionary<int, int>();
}
