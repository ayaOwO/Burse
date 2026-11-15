namespace Common;

public record Trade(int Id = 0)
{
    public List<int> OrderIds { get; init; } = [];
}
