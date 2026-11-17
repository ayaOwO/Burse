namespace Common.Models;

public record Trade(int Id = 0)
{
    public List<Guid> OrderIds { get; init; } = [];
    public int StockId { get; init; }
    public int BuyerId { get; init; }
    public int SellerId { get; init; }
    public int Amount { get; init; }
    public double Price { get; init; }
}
