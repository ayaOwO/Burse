namespace Common;

public record Order
{
    public int Id { get; init; }
    public OrderStatus Status { get; init; }
    public OrderType Type { get; init; }
    public int Price { get; init; }
    public int OwnerId { get; init; }
}

public enum OrderType
{
    Bid,
    Ask
}

public enum OrderStatus
{
    Open,
    Cancelled,
    Filled
}
