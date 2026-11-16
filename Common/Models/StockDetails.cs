using System.ComponentModel.DataAnnotations;

namespace Common.Models;

public sealed record StockOrder(Guid Id, [Range(0, int.MaxValue)] int Owner, OrderType Type, double Price, int Amount, OrderStatus Status);

public sealed record StockDetails(StockInfo Stock, IList<StockOrder> Orders, IList<Trade> LastTrades);
