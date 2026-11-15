namespace Common;

public record Trade(int Id = 0)
{
    public List<Guid> OrderIds { get; init; } = [];
}
