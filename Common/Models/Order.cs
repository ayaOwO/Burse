using System.ComponentModel.DataAnnotations;

namespace Common.Models;

public class Order
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public OrderType Type { get; init; } 
    [Range(0, int.MaxValue)] 
    public double Price { get; init; }
    [Range(1, int.MaxValue)] 
    public int Amount { get; set; }

    [Range(0, int.MaxValue)] public int TraderId { get; init; }
    [Range(1, int.MaxValue)] public int StockId { get; init; }
    public OrderStatus Status { get; set; } =  OrderStatus.Open;
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