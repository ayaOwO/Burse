using BL.Abstractions;
using BL.Services;
using Common.Models;
using DAL;
using DAL.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public class OrderStatusTests
{
    [TestCase(OrderType.Bid, 1, OrderStatus.Filled, OrderStatus.Open, 0, 1)]
    [TestCase(OrderType.Bid, 2, OrderStatus.Filled, OrderStatus.Filled, 0, 0)]
    [TestCase(OrderType.Bid, 4, OrderStatus.Open, OrderStatus.Filled, 2, 0)]
    [TestCase(OrderType.Ask, 1, OrderStatus.Filled, OrderStatus.Open, 0, 1)]
    [TestCase(OrderType.Ask, 2, OrderStatus.Filled, OrderStatus.Filled, 0, 0)]
    [TestCase(OrderType.Ask, 4, OrderStatus.Open, OrderStatus.Filled, 2, 0)]
    public async Task BookingUpdatesFilledOrdersAndReleasesLocks(
        OrderType type, int requested, OrderStatus incomingStatus, OrderStatus restingStatus,
        int incomingRemainder, int restingRemainder)
    {
        var resting = new Order
        {
            Id = Guid.CreateVersion7(DateTimeOffset.UnixEpoch),
            StockId = 1, TraderId = 2, Price = 10, Amount = 2,
            Type = type == OrderType.Bid ? OrderType.Ask : OrderType.Bid
        };
        var provider = new MemoryMarketDataProvider(Options.Create(new MarketDataProviderOptions
        {
            Stocks = [new Stock { Id = 1, CurrentPrice = 10 }],
            Traders = [new Trader(0, "Exchange", 0),
                new Trader(1, "Incoming", 100) { StocksOwned = new Dictionary<int, int> { [1] = 4 } },
                new Trader(2, "Resting", 100) { StocksOwned = new Dictionary<int, int> { [1] = 2 } },
                new Trader(7, "Next", 100) { StocksOwned = new Dictionary<int, int> { [1] = 2 } }],
            Orders = requested <= 2
                ? [resting, new Order { StockId = 1, TraderId = 7, Price = 10, Amount = 2, Type = resting.Type }]
                : [resting]
        }), NullLogger<MemoryMarketDataProvider>.Instance);
        using var stockLock = new SemaphoreSlim(1, 1);
        var stockLocks = new Dictionary<int, SemaphoreSlim> { [1] = stockLock };
        var traderLocks = provider.GetAllTraders().ToDictionary(trader => trader.Id, _ => new SemaphoreSlim(1, 1));
        var incoming = new Order { StockId = 1, TraderId = 1, Price = 10, Amount = requested, Type = type };
        var matcher = new StatusCheckingMatcher(provider);
        var booking = new OrderBookingService(provider, stockLocks, matcher, traderLocks,
            NullLogger<OrderBookingService>.Instance);
        try
        {
            await booking.CreateOrder(incoming);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(matcher.Checked, Is.True);
                Assert.That(resting.Status, Is.EqualTo(restingStatus));
                Assert.That(incoming.Status, Is.EqualTo(incomingStatus));
                Assert.That(provider.GetTradesByTraderId(1).Select(trade => trade.Amount),
                    Is.EqualTo(new[] { requested - incomingRemainder }));
                Assert.That(incoming.Amount, Is.EqualTo(incomingRemainder));
                Assert.That(resting.Amount, Is.EqualTo(restingRemainder));
                Assert.That(stockLocks.Values.Concat(traderLocks.Values).Select(semaphore => semaphore.CurrentCount),
                    Is.All.EqualTo(1));
            }
        }
        finally
        {
            foreach (var semaphore in traderLocks.Values)
                semaphore.Dispose();
        }
    }

    private sealed class StatusCheckingMatcher(IMarketDataProvider provider) : IOrderMatcher
    {
        public bool Checked { get; private set; }

        public List<Trade> MatchOrder(Order incoming)
        {
            var trades = new OrderMatcher(provider, NullLogger<OrderMatcher>.Instance).MatchOrder(incoming);
            Assert.That(incoming.Status, Is.EqualTo(OrderStatus.Open));
            Assert.That(trades.Select(trade => provider.GetOrderById(trade.OrderIds[0]).Status),
                Is.All.EqualTo(OrderStatus.Open));
            Checked = true;
            return trades;
        }
    }
}
