namespace BL.Options;

public sealed class StockUpdaterServiceOptions
{
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(5);
}
