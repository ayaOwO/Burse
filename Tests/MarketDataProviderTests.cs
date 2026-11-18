using Common.Models;
using DAL;
using DAL.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tests;

public class MarketDataProviderTests
{
    [Test]
    public void StockUpdatesAddAndSubtractSharesWithoutChangingEarlierSnapshots()
    {
        var provider = CreateProvider();
        var snapshot = provider.GetTraderById(1);

        provider.UpdateTraderStocks(1, 1, 2);
        provider.UpdateTraderStocks(1, 2, -3);
        provider.UpdateTraderStocks(1, 3, 4);

        Assert.That(provider.GetTraderById(1).StocksOwned,
            Is.EquivalentTo(new Dictionary<int, int> { [1] = 7, [2] = 0, [3] = 4 }));
        Assert.That(snapshot.StocksOwned[1], Is.EqualTo(5));
    }

    [Test]
    public void NegativeHoldingsAreRemovedAndLogged()
    {
        var logger = new CheckLogger();
        var provider = CreateProvider(logger);

        provider.UpdateTraderStocks(1, 1, -6);
        provider.UpdateTraderStocks(1, 3, 5);
        provider.UpdateTraderStocks(1, 4, -1);

        Assert.That(provider.GetAllTraders().Single().StocksOwned,
            Is.EquivalentTo(new Dictionary<int, int> { [2] = 3, [3] = 5 }));
        Assert.That(logger.Warnings, Has.Count.EqualTo(2));
        Assert.That(logger.Warnings[0], Does.Contain("-1"));
    }

    [Test]
    public void MoneyUpdatesAcceptBothSignsAndKeepEarlierSnapshots()
    {
        var provider = CreateProvider();
        var snapshot = provider.GetTraderById(1);

        provider.UpdateTraderMoney(1, 10);
        provider.UpdateTraderMoney(1, -5);

        Assert.That(provider.GetTraderById(1).Money, Is.EqualTo(105));
        Assert.That(snapshot.Money, Is.EqualTo(100));
    }

    [Test]
    public void ExchangeStartsWithAllSharesAndListsThemForSale()
    {
        var provider = CreateExchangeProvider();
        var stocks = provider.GetAllStocks();

        Assert.That(provider.GetTraderById(0).StocksOwned,
            Is.EquivalentTo(stocks.ToDictionary(stock => stock.Id, stock => stock.Amount)));
        Assert.That(provider.GetOrdersByTraderId(0).Select(order => (order.StockId, order.Amount, order.Type, order.Price)),
            Is.EquivalentTo(stocks.Select(stock => (stock.Id, stock.Amount, OrderType.Ask, stock.CurrentPrice))));
    }

    [Test]
    public void HoldingsValueUsesCurrentPricesAndQuantities()
    {
        var provider = CreateExchangeProvider();
        Assert.That(provider.GetTraderHoldingsValue(0), Is.EqualTo(80));

        provider.UpdateTraderStocks(0, 1, -10);
        Assert.That(provider.GetTraderHoldingsValue(0), Is.EqualTo(60));

        provider.UpdateStockPrice(2, 4);
        Assert.That(provider.GetTraderHoldingsValue(0), Is.EqualTo(80));

        provider.UpdateTraderStocks(0, 2, -20);
        Assert.That(provider.GetTraderHoldingsValue(0), Is.Zero);
    }

    private static IMarketDataProvider CreateProvider(CheckLogger? logger = null) =>
        new MemoryMarketDataProvider(Options.Create(new MarketDataProviderOptions
        {
            Traders = [new Trader(1, "Trader", 100) { StocksOwned = new Dictionary<int, int> { [1] = 5, [2] = 3 } }]
        }), logger ?? new CheckLogger());

    private static IMarketDataProvider CreateExchangeProvider() =>
        new MemoryMarketDataProvider(Options.Create(new MarketDataProviderOptions
        {
            Stocks = [new Stock { Id = 1, Amount = 10, CurrentPrice = 2 },
                new Stock { Id = 2, Amount = 20, CurrentPrice = 3 }],
            Traders = [new Trader(0, "Exchange", 0)]
        }), new CheckLogger());

    private sealed class CheckLogger : ILogger<MemoryMarketDataProvider>
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
