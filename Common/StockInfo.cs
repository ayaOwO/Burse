namespace Common;

public sealed class StockInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrentPrice { get; set; }
    public int Amount { get; set; }
}
