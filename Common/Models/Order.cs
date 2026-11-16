using System.ComponentModel.DataAnnotations;

namespace Common.Models;

public class Order()
{
    public Guid Id { get; init; }
    public OrderType Type { get; init; }
    public double Price { get; init; }
    public int Amount { get; set; }

    [Range(0, int.MaxValue)] public int OwnerId { get; init; }
    [Range(1, int.MaxValue)] public int StockId { get; init; }
    public OrderStatus Status { get; set; }
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