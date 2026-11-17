using Common.Models;

namespace BL.Abstractions;

public interface IStockUpdater
{
    public double CalculatePrice(Stock stock);
}
