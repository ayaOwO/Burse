namespace Common;

public record Trader
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public int Money { get; init; }
    public int InitialMoney { get; init; }
}