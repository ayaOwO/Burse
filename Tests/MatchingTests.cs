using BL.Services;
using Common.Exceptions;
using Common.Models;
using DAL;
using DAL.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public class MatchingTests
{
    public static IEnumerable<TestCaseData> FillCases()
    {
        yield return FillCase("Cheapest asks first across multiple fills", 10, [Ask(6, 1), Ask(5, 2), Ask(5, 3)], [0, 1, 5]);
        yield return FillCase("Older ask wins at the same price", 6, [Ask(6, 1, 2), Ask(6, 1, 1)], [6, 0]);
        yield return FillCase("Older ask fills before the newer ask", 8, [Ask(6, 1, 2), Ask(6, 1, 1)], [4, 0]);
        yield return FillCase("Ask price takes priority over age", 4, [Ask(4, 2, 1), Ask(4, 1, 2)], [4, 0]);
        yield return FillCase("Larger resting ask fills partially", 10, [Ask(15, 1)], [5]);
        yield return FillCase("Unfilled incoming quantity remains open", 10, [Ask(6, 1), Ask(3, 2)], [0, 0]);
        yield return FillCase("Empty book leaves the incoming order open", 10, [], []);
        yield return FillCase("Ignores closed asks, zero quantity, and asks above the limit", 10,
            [Ask(10, 21), Ask(10, 1, status: OrderStatus.Cancelled), Ask(10, 1, status: OrderStatus.Filled), Ask(0, 1)],
            [10, 10, 10, 0]);
        yield return FillCase("Ignores asks on a different stock", 10,
            [new Order { StockId = 2, Type = OrderType.Ask, Amount = 10, Price = 1 }], [10]);
        yield return FillCase("Highest bid first", 10, [Bid(10, 9), Bid(10, 11), Bid(10, 12)], [10, 10, 0], OrderType.Ask, 10);
        yield return FillCase("Older bid wins at the same price", 8, [Bid(6, 12, 2), Bid(6, 12, 1)], [4, 0], OrderType.Ask, 10);
        yield return FillCase("Allows the largest whole-share quantity", int.MaxValue, [Ask(int.MaxValue, 1)], [0]);
        yield return FillCase("Buy limit is inclusive", 5, [Ask(10, 20)], [5]);
        yield return FillCase("Sell limit is inclusive", 5, [Bid(10, 10)], [5], OrderType.Ask, 10);
        yield return FillCase("Only the opposite side can match", 5, [Bid(5, 1), Ask(5, 1)], [5, 0]);
        yield return FillCase("Sell ignores bids below the limit", 5, [Ask(5, 20), Bid(5, 9)], [5, 5], OrderType.Ask, 10);
        yield return FillCase("Fractional prices respect the limit", 5, [Ask(5, 10.26), Ask(5, 10.25)], [5, 0], limit: 10.25);
        yield return FillCase("Negative resting quantity cannot trade", 5, [Ask(-1, 1), Ask(5, 1)], [-1, 0]);

        var lowerId = new Order { Id = Guid.Parse("018bcfe5-6800-7000-8000-000000000001"), Type = OrderType.Ask, StockId = 1, Amount = 1, Price = 1 };
        var higherId = new Order { Id = Guid.Parse("018bcfe5-6800-7000-8000-000000000002"), Type = OrderType.Ask, StockId = 1, Amount = 1, Price = 1 };
        yield return FillCase("Equal timestamps break ties by GUID", 1, [higherId, lowerId], [1, 0]);
    }

    private static TestCaseData FillCase(string name, int requested, List<Order> orders, int[] remaining,
        OrderType type = OrderType.Bid, double limit = 20) =>
        new TestCaseData(requested, orders, remaining, type, limit).SetName($"Matching: {name}");

    [TestCaseSource(nameof(FillCases))]
    public async Task MatchesOrdersInPriceTimePriority(int requested, List<Order> orders, int[] remaining,
        OrderType type, double limit)
    {
        var incoming = new Order
        {
            Id = Guid.CreateVersion7(), StockId = 1, TraderId = 1, Type = type, Amount = requested, Price = limit
        };
        var original = orders.Select(order => (order.Amount, order.Status, order.Id, order.Type,
            order.Price, order.TraderId, order.StockId)).ToArray();
        var provider = MatchingProvider(orders, type == OrderType.Ask ? requested : 0);
        await BookOrderAsync(CreateBooking(provider), incoming);
        var result = provider.GetTradesByStockId(1);
        Assert.That(result.All(trade => trade.Amount > 0), Is.True, "Every execution fills a positive quantity");
        Assert.That(result.Sum(trade => (long)trade.Amount) == requested - (long)incoming.Amount, Is.True, "Trades contain all fills");
        Assert.That(result.All(trade => trade.OrderIds[1] == incoming.Id && trade.StockId == incoming.StockId
            && trade.Price == orders.Single(order => order.Id == trade.OrderIds[0]).Price
            && trade.BuyerId == (type == OrderType.Bid ? 1 : 0)
            && trade.SellerId == (type == OrderType.Ask ? 1 : 0)), Is.True, "Trade identities and execution prices");
        Assert.That(orders.Select(order => order.Amount).SequenceEqual(remaining), Is.True, "Expected quantities at each price and time");
        var filled = original.Select((item, index) => item.Amount - remaining[index]).Sum();
        Assert.That(incoming.Amount == requested - filled, Is.True, "Incoming remainder");
        for (var index = 0; index < orders.Count; index++)
        {
            Assert.That(orders[index].Status == (original[index].Amount > remaining[index] && remaining[index] == 0
                ? OrderStatus.Filled : original[index].Status), Is.True, "Filled, partial, and untouched statuses");
            var order = orders[index];
            var before = original[index];
            Assert.That((order.Id, order.Type, order.Price, order.TraderId, order.StockId)
                == (before.Id, before.Type, before.Price, before.TraderId, before.StockId), Is.True, "Order identity and terms unchanged");
        }
        Assert.That(incoming.Status == (incoming.Amount == 0 ? OrderStatus.Filled : OrderStatus.Open), Is.True, "Incoming status");
        Assert.That(provider.GetOrdersByTraderId(1).Single().Id == incoming.Id, Is.True, "Incoming order is stored after matching");
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void RejectsInvalidQuantityWithoutChangingOrders(int amount)
    {
        var resting = Ask(10, 1);
        var provider = MatchingProvider([resting]);
        var incoming = new Order { Id = Guid.CreateVersion7(), StockId = 1, TraderId = 1, Type = OrderType.Bid, Price = 20, Amount = amount };
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateBooking(provider).CreateOrder(incoming));
        Assert.That(resting.Amount == 10 && resting.Status == OrderStatus.Open, Is.True, "Invalid request leaves resting order unchanged");
        Assert.That(incoming.Amount == amount && incoming.Status == OrderStatus.Open, Is.True, "Invalid request leaves incoming order unchanged");
        Assert.That(provider.GetOrdersByTraderId(1), Is.Empty);
    }

    [Test]
    public async Task RepeatedMatchingUsesOnlyRemainingQuantities()
    {
        var older = Ask(10, 1, 1);
        var newer = Ask(10, 1, 2);
        var provider = MatchingProvider([newer, older]);
        var service = CreateBooking(provider);
        var first = Bid(4, 1, traderId: 1);
        await BookOrderAsync(service, first);
        Assert.That(older.Amount == 6 && older.Status == OrderStatus.Open && newer.Amount == 10, Is.True, "Partial fill keeps older order open");
        var second = Bid(8, 1, traderId: 1);
        await BookOrderAsync(service, second);
        Assert.That(older.Amount == 0 && older.Status == OrderStatus.Filled, Is.True, "Older remainder filled on second match");
        Assert.That(newer.Amount == 8 && newer.Status == OrderStatus.Open, Is.True, "Second match partially fills newer order");
        Assert.That(first.Amount == 0 && first.Status == OrderStatus.Filled
            && second.Amount == 0 && second.Status == OrderStatus.Filled, Is.True, "Both incoming orders filled");
        var third = Bid(10, 1, traderId: 1);
        await BookOrderAsync(service, third);
        var result = provider.GetTradesByStockId(1).Where(trade => trade.OrderIds.Contains(third.Id)).ToList();
        Assert.That(older.Amount == 0 && newer.Amount == 0 && newer.Status == OrderStatus.Filled, Is.True, "Later matching consumes only remaining quantities");
        Assert.That(result.Sum(trade => trade.Amount) == 8 && third.Amount == 2 && third.Status == OrderStatus.Open,
            Is.True, "Later matching returns the open remainder");
        Assert.That(provider.GetOrdersByType(OrderType.Bid).Count == 3, Is.True, "Repeated matching stores all incoming orders");
    }

    [Test]
    public async Task SettlementUsesRestingPriceAndReleasesReservations()
    {
        var provider = SettlementProvider();
        var service = CreateBooking(provider);
        var ask = await BookOrderAsync(service, new Order { TraderId = 2, Type = OrderType.Ask, StockId = 1, Price = 10, Amount = 6 });
        Assert.ThrowsAsync<InsufficientStocksException>(() => service.CreateOrder(new Order { TraderId = 2, Type = OrderType.Ask, StockId = 1, Price = 10, Amount = 5 }));
        var bid = await BookOrderAsync(service, new Order { TraderId = 1, Type = OrderType.Bid, StockId = 1, Price = 12, Amount = 4 });
        Assert.That(bid.Status == OrderStatus.Filled && ask.Amount == 2, Is.True, "Filled buy and partial ask");
        Assert.That(provider.GetStockCountOfOpenOrdersByTraders(2, 1) == 2 && provider.GetStockCountOfOpenOrdersByTraders(1, 1) == 0,
            Is.True, "Listed count includes only trader's remaining open asks");
        Assert.That(provider.GetStocks(1).CurrentPrice == 10, Is.True, "Incoming buy updates price to resting ask price");
        Assert.That(provider.GetTraderById(1).Money == 60 && provider.GetTraderById(2).Money == 40, Is.True, "Money uses resting price");
        Assert.That(provider.GetTraderById(1).StocksOwned[1] == 4 && provider.GetTraderById(2).StocksOwned[1] == 6, Is.True, "Both holdings settled");
        var trade = provider.GetTradesByTraderId(1).Single();
        Assert.That(trade.Amount == 4 && trade.Price == 10 && trade.OrderIds.Contains(bid.Id)
            && provider.GetTradesByStockId(1).Single().Id == trade.Id, Is.True, "Filled incoming order retained in history");
        Assert.Throws<InvalidOperationException>(() => service.CancelOrder(bid.Id));
        service.CancelOrder(ask.Id);
        Assert.That(provider.GetStockCountOfOpenOrdersByTraders(2, 1) == 0, Is.True, "Cancelled asks release listed quantities");
        var replacement = await BookOrderAsync(service, new Order { TraderId = 2, Type = OrderType.Ask, StockId = 1, Price = 15, Amount = 6 });
        service.CancelOrder(replacement.Id);
        var restingBid = await BookOrderAsync(service, new Order { TraderId = 1, Type = OrderType.Bid, StockId = 1, Price = 12, Amount = 5 });
        Assert.ThrowsAsync<InsufficientFundsException>(() => service.CreateOrder(new Order { TraderId = 1, Type = OrderType.Bid, StockId = 1, Price = 12, Amount = 1 }));
        var sell = await BookOrderAsync(service, new Order { TraderId = 2, Type = OrderType.Ask, StockId = 1, Price = 10, Amount = 3 });
        Assert.That(sell.Status == OrderStatus.Filled && restingBid.Amount == 2, Is.True, "Sell fills resting bid partially");
        Assert.That(provider.GetCommittedMoney(1) == 24 && provider.GetCommittedMoney(2) == 0,
            Is.True, "Committed money includes only trader's remaining open bids");
        Assert.That(provider.GetStocks(1).CurrentPrice == 12, Is.True, "Incoming sell updates price to resting bid price");
        Assert.That(provider.GetTraderById(1).Money == 24 && provider.GetTraderById(2).Money == 76
            && provider.GetTraderById(1).StocksOwned[1] == 7 && provider.GetTraderById(2).StocksOwned[1] == 3,
            Is.True, "Sell settles both parties at resting bid price");
        Assert.That(provider.GetTradesByTraderId(2).Count == 2, Is.True, "Both fills persisted");
        service.CancelOrder(restingBid.Id);
        Assert.That(provider.GetCommittedMoney(1) == 0, Is.True, "Cancelled bids release committed money");
        await BookOrderAsync(service, new Order { TraderId = 1, Type = OrderType.Bid, StockId = 1, Price = 12, Amount = 2 });
    }

    [Test]
    public async Task LastExecutionPriceUpdatesQuoteAcrossMultipleFills()
    {
        var provider = SettlementProvider();
        var service = CreateBooking(provider);
        await BookOrderAsync(service, new Order { TraderId = 2, Type = OrderType.Ask, StockId = 1, Price = 3, Amount = 2 });
        await BookOrderAsync(service, new Order { TraderId = 2, Type = OrderType.Ask, StockId = 1, Price = 4, Amount = 2 });
        Assert.That(provider.GetStocks(1).CurrentPrice == 100, Is.True, "Unmatched orders leave price unchanged");
        var bid = await BookOrderAsync(service, new Order { TraderId = 1, Type = OrderType.Bid, StockId = 1, Price = 5, Amount = 5 });
        Assert.That(bid.Amount == 1 && provider.GetStocks(1).CurrentPrice == 4,
            Is.True, "Last execution price wins across multiple fills, rather than incoming limit");
    }

    [TestCase(OrderType.Bid)]
    [TestCase(OrderType.Ask)]
    public async Task RejectsOppositeOpenOrderUntilFirstOrderIsCancelled(OrderType type)
    {
        var provider = SettlementProvider();
        provider.UpdateTraderStocks(1, 1, 5);
        var service = CreateBooking(provider);
        var first = await BookOrderAsync(service, new Order { TraderId = 1, Type = type, StockId = 1, Price = 10, Amount = 1 });
        var opposite = type == OrderType.Bid ? OrderType.Ask : OrderType.Bid;
        Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOrder(new Order { TraderId = 1, Type = opposite, StockId = 1, Price = 10, Amount = 1 }));
        Assert.That(provider.GetOrdersByTraderId(1).Count == 1 && first.Amount == 1
            && provider.GetTraderById(1).Money == 100 && provider.GetTraderById(1).StocksOwned[1] == 5,
            Is.True, "Opposite offer rejected without changing orders or balances");
        service.CancelOrder(first.Id);
        await BookOrderAsync(service, new Order { TraderId = 1, Type = opposite, StockId = 1, Price = 10, Amount = 1 });
        Assert.That(provider.GetOrdersByTraderId(1).Count == 2, Is.True, "Cancelled offer does not block opposite offer");
    }

    [Test]
    public async Task TraderStatsTrackPortfolioValueAndReturnLatestEightTrades()
    {
        var provider = new MemoryMarketDataProvider(Options.Create(new MarketDataProviderOptions
        {
            Stocks = [new Stock { Id = 1, CurrentPrice = 10, Amount = 20 },
                new Stock { Id = 2, CurrentPrice = 5, Amount = 0 }],
            Traders = [new Trader(0, "Exchange", 0),
                new Trader(1, "Buyer", 1000) { StocksOwned = new Dictionary<int, int> { [2] = 2 } }]
        }), NullLogger<MemoryMarketDataProvider>.Instance);
        var traders = new TraderService(provider);
        Assert.That(traders.GetTrades(1).ProfitLoss == 0 && traders.GetTrades(0).ProfitLoss == 0,
            Is.True, "Opening portfolios include initial holdings and exchange shares");
        var orders = CreateBooking(provider);
        for (var i = 0; i < 12; i++) await BookOrderAsync(orders, new Order { TraderId = 1, Type = OrderType.Bid, StockId = 1, Price = 10, Amount = 1 });
        var result = traders.GetTrades(1);
        Assert.That(result.Trades.Select(trade => trade.Id).SequenceEqual(Enumerable.Range(5, 8).Reverse())
            && provider.GetTradesByTraderId(1).Count == 12, Is.True, "Latest eight trades returned without trimming stored history");
        Assert.That(result.ProfitLoss == 0 && result.CurrentBalance == 880, Is.True, "Buying shares is not a portfolio loss");
        provider.UpdateStockPrice(1, 12);
        Assert.That(traders.GetTrades(1).ProfitLoss == 24, Is.True, "Price appreciation includes all held shares");
        provider.UpdateStockPrice(1, 8);
        Assert.That(traders.GetTrades(1).ProfitLoss == -24, Is.True, "Price depreciation produces portfolio loss");
        provider.UpdateStockPrice(2, 7);
        Assert.That(traders.GetTrades(1).ProfitLoss == -20, Is.True, "Starting holdings contribute price changes to profit/loss");
    }

    [Test]
    public async Task ConcurrentOrdersConserveCashAndSharesAcrossBookingInstances()
    {
        var provider = SettlementProvider();
        using var stockSemaphore = new SemaphoreSlim(1, 1);
        var stockSemaphores = new Dictionary<int, SemaphoreSlim> { [1] = stockSemaphore };
        var traderSemaphores = provider.GetAllTraders().ToDictionary(trader => trader.Id, _ => new SemaphoreSlim(1, 1));
        try
        {
            var asks = await SubmitConcurrently<InsufficientStocksException>(2, OrderType.Ask);
            Assert.That(asks.Count(accepted => accepted), Is.EqualTo(10));
            Assert.That(provider.GetOrdersByTraderId(2).Sum(order => order.Amount), Is.EqualTo(10));

            var bids = await SubmitConcurrently<InsufficientFundsException>(1, OrderType.Bid);
            Assert.That(bids.Count(accepted => accepted), Is.EqualTo(10));

            var buyer = provider.GetTraderById(1);
            var seller = provider.GetTraderById(2);
            var trades = provider.GetTradesByStockId(1);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(buyer.Money, Is.Zero);
                Assert.That(seller.Money, Is.EqualTo(100));
                Assert.That(buyer.StocksOwned[1], Is.EqualTo(10));
                Assert.That(seller.StocksOwned[1], Is.Zero);
                Assert.That(trades, Has.Count.EqualTo(10));
                Assert.That(trades.Select(trade => trade.Id), Is.Unique);
            }
        }
        finally
        {
            foreach (var semaphore in traderSemaphores.Values)
                semaphore.Dispose();
        }

        async Task<bool[]> SubmitConcurrently<TException>(int traderId, OrderType side) where TException : Exception
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var submissions = Enumerable.Range(0, 20).Select(_ => Submit()).ToArray();
            start.SetResult();
            return await Task.WhenAll(submissions).WaitAsync(TimeSpan.FromSeconds(10));

            async Task<bool> Submit()
            {
                await start.Task;
                var service = CreateBooking(provider, stockSemaphores, traderSemaphores);
                try
                {
                    await service.CreateOrder(new Order { TraderId = traderId, Type = side, StockId = 1, Price = 10, Amount = 1 });
                    return true;
                }
                catch (TException)
                {
                    return false;
                }
            }
        }
    }

    private static async Task<Order> BookOrderAsync(OrderBookingService booking, Order order)
    {
        await booking.CreateOrder(order);
        return order;
    }

    private static OrderBookingService CreateBooking(IMarketDataProvider provider,
        IDictionary<int, SemaphoreSlim>? semaphores = null, IDictionary<int, SemaphoreSlim>? traderSemaphores = null)
    {
        if (semaphores is null)
        {
            semaphores = new Dictionary<int, SemaphoreSlim>();
            foreach (var stock in provider.GetAllStocks())
                semaphores[stock.Id] = new SemaphoreSlim(1, 1);
        }

        traderSemaphores ??= provider.GetAllTraders().ToDictionary(trader => trader.Id, _ => new SemaphoreSlim(1, 1));
        return new OrderBookingService(provider, semaphores,
            new OrderMatcher(provider, NullLogger<OrderMatcher>.Instance), traderSemaphores,
            NullLogger<OrderBookingService>.Instance);
    }

    private static MemoryMarketDataProvider MatchingProvider(List<Order> orders, int incomingShares = 0)
    {
        var provider = new MemoryMarketDataProvider(Options.Create(new MarketDataProviderOptions
        {
            Stocks = [new Stock { Id = 1 }, new Stock { Id = 2 }],
            Traders = [new Trader(0, "Resting trader", 1e15), new Trader(1, "Incoming trader", 1e15)],
            Orders = orders
        }), NullLogger<MemoryMarketDataProvider>.Instance);
        foreach (var stock in orders.Where(order => order.Type == OrderType.Ask && order.Amount > 0)
                     .GroupBy(order => order.StockId))
            provider.UpdateTraderStocks(0, stock.Key, stock.Sum(order => order.Amount));
        provider.UpdateTraderStocks(1, 1, incomingShares);
        return provider;
    }

    private static MemoryMarketDataProvider SettlementProvider() => new(Options.Create(new MarketDataProviderOptions
    {
        Stocks = [new Stock { Id = 1, CurrentPrice = 100, Amount = 0 }],
        Traders = [new Trader(0, "Burse", 0), new Trader(1, "Buyer", 100),
            new Trader(2, "Seller", 0) { StocksOwned = new Dictionary<int, int> { [1] = 10 } }]
    }), NullLogger<MemoryMarketDataProvider>.Instance);

    private static Order Ask(int amount, double price, int millisecond = 0, OrderStatus status = OrderStatus.Open) => new()
    {
        Id = Guid.CreateVersion7(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000L + millisecond)),
        StockId = 1, Type = OrderType.Ask, Amount = amount, Price = price, Status = status
    };

    private static Order Bid(int amount, double price, int millisecond = 0, int traderId = 0) => new()
    {
        Id = Guid.CreateVersion7(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000L + millisecond)),
        StockId = 1, TraderId = traderId, Type = OrderType.Bid, Amount = amount, Price = price
    };
}
