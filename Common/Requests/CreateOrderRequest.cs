using Common;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Common.Requests;

public sealed class CreateOrderRequest
{
    [JsonPropertyName("trader_id")]
    [Range(1, int.MaxValue)]
    public required int TraderId { get; init; }
    [EnumDataType(typeof(OrderType))]
    public required OrderType Type { get; init; }
    [JsonPropertyName("stock_id")]
    [Range(1, int.MaxValue)]
    public required int StockId { get; init; }
    [Range(0.0000001, double.MaxValue)]
    public required double Price { get; init; }
    [Range(1, int.MaxValue)]
    public int Amount { get; init; } = 1;
}
