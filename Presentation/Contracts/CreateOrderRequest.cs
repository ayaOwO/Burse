using Common;
using System.Text.Json.Serialization;

namespace Presentation.Contracts;

public sealed class CreateOrderRequest
{
    [JsonPropertyName("trader_id")]
    public required int TraderId { get; init; }
    public required OrderType Type { get; init; }
    [JsonPropertyName("stock_id")]
    public required int StockId { get; init; }
    public required double Price { get; init; }
    public int Amount { get; init; } = 1;
}
