using BL.Abstractions;
using BL.Options;
using Common.Models;
using DAL;
using Microsoft.Extensions.Options;

namespace Presentation.Services;

public sealed class StockUpdaterService : BackgroundService
{
    private readonly TimeSpan _tickInterval;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IStockUpdater _stockUpdater;
    private readonly ILogger<StockUpdaterService> _logger;

    public StockUpdaterService(IMarketDataProvider marketDataProvider, IStockUpdater stockUpdater,
        IOptions<StockUpdaterServiceOptions> options, ILogger<StockUpdaterService> logger)
    {
        _marketDataProvider = marketDataProvider;
        _stockUpdater = stockUpdater;
        _logger = logger;
        _tickInterval = options.Value.TickInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_tickInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var stock in _marketDataProvider.GetAllStocks())
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    var newPrice = _stockUpdater.CalculatePrice(new Stock(stock));
                    _logger.LogInformation("Stock {StockName} price changed from {OldPrice} to {StockCurrentPrice}",
                        stock.Name, stock.CurrentPrice, newPrice);
                    _marketDataProvider.UpdateStockPrice(stock.Id, newPrice);
                }
            }
        }
        catch (OperationCanceledException e)
        {
            _logger.LogError(e, "Operation Cancellation requested, not all stock prices updated");
        }
    }
}
