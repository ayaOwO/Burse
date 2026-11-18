namespace Common.Models;

public sealed record StockSummary(Stock Stock, IList<Order> Orders, IList<Trade> LastTrades);
