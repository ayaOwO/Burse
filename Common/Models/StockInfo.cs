using System.ComponentModel.DataAnnotations;

namespace Common.Models;

public sealed class StockInfo
{
    public StockInfo() { }

    public StockInfo(StockInfo stock)
    {
        Id = stock.Id;
        Name = stock.Name;
        CurrentPrice = stock.CurrentPrice;
        Amount = stock.Amount;
    }

    [Range(1, int.MaxValue)]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CurrentPrice { get; set; }
    public int Amount { get; set; }
}
