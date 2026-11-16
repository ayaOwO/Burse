using System.ComponentModel.DataAnnotations;

namespace Common;
public record Order(
    Guid Id,
    OrderType Type,
    double Price,
    int Amount,
    int OwnerId,
    [Range(1, int.MaxValue)] int StockId,
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
