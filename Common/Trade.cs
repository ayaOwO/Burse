namespace Common;

public record Trade
{
    public int Id { get; init; }
    public List<int> OrderIds { get; init; } = [];
}
