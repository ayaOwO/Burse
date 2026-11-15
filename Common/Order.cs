namespace Common;
public record Order(
    Guid Id,
    OrderType Type,
    int Price,
    int Amount,
    int OwnerId,
    int StockId,
    OrderStatus Status = OrderStatus.Open
    );

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
