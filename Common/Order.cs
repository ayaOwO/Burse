namespace Common;

public record Order(
    int Id,
    OrderStatus Status,
    OrderType Type,
    int Price,
    int OwnerId,
    int StockId);

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
