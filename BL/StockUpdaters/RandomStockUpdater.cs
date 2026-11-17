using BL.Abstractions;
using BL.Options;
using Common.Models;
using Microsoft.Extensions.Options;

namespace BL.StockUpdaters;

public sealed class RandomStockUpdater(IOptions<RandomStockUpdaterOptions> options) : IStockUpdater
{
    private readonly int _factor = options.Value.Factor;
    private readonly int _offset = options.Value.Offset;

    public double CalculatePrice(Stock stock)
    {
        int changePercent = Random.Shared.Next(-1 * _factor + _offset, _factor + 1);
        var price = stock.CurrentPrice * (1 + changePercent / 100.0);
        return Math.Clamp(price, 1, double.MaxValue);
    }
}
