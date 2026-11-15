namespace Common;

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

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrentPrice { get; set; }
    public int Amount { get; set; }
}
