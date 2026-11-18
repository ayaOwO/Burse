using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BL.Abstractions;
using BL.Services;
using Common;
using Common.Models;
using DAL;
using DAL.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Presentation.Controllers;
using Presentation.ExceptionHandlers;

namespace Tests;

public class EndpointTests
{
    private WebApplication app = null!;
    private HttpClient client = null!;
    private Guid buyId;

    private IMarketDataProvider Provider => app.Services.GetRequiredService<IMarketDataProvider>();

    [SetUp]
    public async Task StartHost()
    {
        buyId = Guid.NewGuid();
        var sellId = Guid.NewGuid();
        var data = new MarketDataProviderOptions
        {
            Stocks = [new Stock { Id = 1, Name = "Test", CurrentPrice = 10.25, Amount = 100 }],
            Traders =
            [
                new Trader(0, "Burse", 0),
                new Trader(1, "Buyer", 100.5) { StocksOwned = new Dictionary<int, int> { [1] = 7 } },
                new Trader(2, "Seller", 50)
            ],
            Orders =
            [
                new Order { Id = buyId, Type = OrderType.Bid, Price = 10.25, TraderId = 1, StockId = 1, Status = OrderStatus.Filled },
                new Order { Id = sellId, Type = OrderType.Ask, Price = 10.25, TraderId = 0, StockId = 1, Status = OrderStatus.Filled }
            ],
            Trades = Enumerable.Range(1, 12).Select(id => new Trade(id) { OrderIds = [buyId, sellId] }).ToList()
        };
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(Options.Create(data));
        builder.Services.AddSingleton<IMarketDataProvider, MemoryMarketDataProvider>();
        builder.Services.AddScoped<IStockService, StockService>();
        builder.Services.AddScoped<ITraderService, TraderService>();
        builder.Services.AddKeyedSingleton(ServiceKeys.StockSemaphores, Presentation.Bootstrap.CreateSemaphores);
        builder.Services.AddKeyedSingleton(ServiceKeys.TraderSemaphores, Presentation.Bootstrap.CreateSemaphores);
        builder.Services.AddScoped<IOrderMatcher, OrderMatcher>();
        builder.Services.AddScoped<IOrderBookingService, OrderBookingService>();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(StocksController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        app = builder.Build();
        app.UseExceptionHandler();
        app.MapControllers();
        app.MapGet("/checks/missing", MissingResource);
        app.MapGet("/checks/unexpected", UnexpectedFailure);
        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    }

    [TearDown]
    public async Task StopHost()
    {
        client?.Dispose();
        if (app is not null)
            await app.DisposeAsync();
    }

    [Test]
    public void OwnershipOptionsBindStockQuantities()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Traders:0:Id"] = "1",
            ["Traders:0:Name"] = "Buyer",
            ["Traders:0:Money"] = "100",
            ["Traders:0:StocksOwned:1"] = "7"
        }).Build();
        var bound = new MarketDataProviderOptions();
        configuration.Bind(bound);

        Assert.That(bound.Traders.Single(trader => trader.Id == 1).StocksOwned[1], Is.EqualTo(7));
    }

    [Test]
    public async Task StocksListIncludesFractionalPrices()
    {
        var stocks = await Get("/stocks");

        Assert.That(stocks.GetArrayLength(), Is.EqualTo(1));
        Assert.That(stocks[0].GetProperty("currentPrice").GetDouble(), Is.EqualTo(10.25));
    }

    [Test]
    public async Task StockDetailsIncludeOnlyOpenOrdersAndTenLatestTrades()
    {
        var stock = await Get("/stocks/1");
        var orders = stock.GetProperty("orders");
        var trades = stock.GetProperty("lastTrades");

        Assert.That(stock.GetProperty("stock").GetProperty("id").GetInt32(), Is.EqualTo(1));
        Assert.That(orders.GetArrayLength(), Is.EqualTo(1));
        Assert.That(orders[0].GetProperty("traderId").GetInt32(), Is.Zero);
        Assert.That(orders[0].GetProperty("stockId").GetInt32(), Is.EqualTo(1));
        Assert.That(trades.EnumerateArray().Select(trade => trade.GetProperty("id").GetInt32()),
            Is.EqualTo(Enumerable.Range(3, 10).Reverse()));
    }

    [Test]
    public async Task TraderListOmitsOpeningBalances()
    {
        var traders = await Get("/traders");

        Assert.That(traders.GetArrayLength(), Is.EqualTo(3));
        Assert.That(traders.EnumerateArray().All(trader => !trader.TryGetProperty("initialMoney", out _)),
            Is.True);
    }

    [TestCase(1, 100.5, 7)]
    [TestCase(0, 0.0, 100)]
    public async Task TraderDetailsIncludeBalanceAndHoldings(int traderId, double money, int stocksOwned)
    {
        var trader = await Get($"/traders/{traderId}");

        Assert.That(trader.GetProperty("money").GetDouble(), Is.EqualTo(money));
        Assert.That(trader.GetProperty("stocksOwned").GetProperty("1").GetInt32(), Is.EqualTo(stocksOwned));
    }

    [Test]
    public async Task TraderHistoryIncludesLatestEightTradesAndBalances()
    {
        var history = await Get("/traders/trades/1");

        Assert.That(history.GetProperty("trades").EnumerateArray().Select(trade => trade.GetProperty("id").GetInt32()),
            Is.EqualTo(Enumerable.Range(5, 8).Reverse()));
        Assert.That(history.GetProperty("currentBalance").GetDouble(), Is.EqualTo(100.5));
        Assert.That(history.GetProperty("profitLoss").GetDouble(), Is.Zero);
    }

    [Test]
    public async Task UnmatchedOrderIsStoredWithoutSettlement()
    {
        var orderId = await CreateBid();
        var order = Provider.GetOrdersByTraderId(1).Single(order => order.Id == orderId);

        Assert.That(order.Amount, Is.EqualTo(3));
        Assert.That(order.Price, Is.EqualTo(9));
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Open));
        Assert.That(Provider.GetTraderById(1).Money, Is.EqualTo(100.5));
    }

    [Test]
    public void TraderOwnershipSnapshotDoesNotChangeStoredHoldings()
    {
        Provider.GetTraderById(1).StocksOwned[1] = 99;

        Assert.That(Provider.GetTraderById(1).StocksOwned[1], Is.EqualTo(7));
    }

    [Test]
    public async Task CancellingAnOpenOrderHidesItAndAllowsRepeatedCancellation()
    {
        var orderId = await CreateBid();
        using var cancelled = await client.DeleteAsync($"/orders/{orderId}");
        using var repeated = await client.DeleteAsync($"/orders/{orderId}");
        var stock = await Get("/stocks/1");

        Assert.That(cancelled.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(repeated.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(Provider.GetOrdersByTraderId(1).Single(order => order.Id == orderId).Status,
            Is.EqualTo(OrderStatus.Cancelled));
        Assert.That(stock.GetProperty("orders").GetArrayLength(), Is.EqualTo(1));
    }

    [TestCase(true, HttpStatusCode.Conflict)]
    [TestCase(false, HttpStatusCode.NotFound)]
    public async Task CancellationRejectsFilledOrMissingOrders(bool filled, HttpStatusCode expectedStatus)
    {
        using var response = await client.DeleteAsync($"/orders/{(filled ? buyId : Guid.NewGuid())}");

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
    }

    [Test]
    public async Task OrderAmountDefaultsToOne()
    {
        using var response = await Post("""{"trader_id":1,"type":"Ask","stock_id":1,"price":11.5}""");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.That(Provider.GetOrdersByTraderId(1).Single(order => order.Id == id).Amount, Is.EqualTo(1));
    }

    [TestCase("/stocks/999")]
    [TestCase("/traders/999")]
    [TestCase("/traders/trades/999")]
    public async Task MissingResourcesReturnNotFound(string route)
    {
        using var response = await client.GetAsync(route);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("""{"trader_id":1,"type":"Bid","stock_id":1,"price":0}""", HttpStatusCode.BadRequest)]
    [TestCase("""{"trader_id":1,"type":"Bid","stock_id":1,"price":1,"amount":0}""", HttpStatusCode.BadRequest)]
    [TestCase("""{"trader_id":1,"type":"Bid","stock_id":1,"price":"NaN"}""", HttpStatusCode.BadRequest)]
    [TestCase("""{"trader_id":1,"type":99,"stock_id":1,"price":1}""", HttpStatusCode.BadRequest)]
    [TestCase("""{"trader_id":1,"stock_id":1,"price":1}""", HttpStatusCode.BadRequest)]
    [TestCase("""{"trader_id":999,"type":"Bid","stock_id":1,"price":1}""", HttpStatusCode.NotFound)]
    [TestCase("""{"trader_id":1,"type":"Bid","stock_id":999,"price":1}""", HttpStatusCode.NotFound)]
    public async Task InvalidOrdersAreRejectedWithoutBeingStored(string json, HttpStatusCode expectedStatus)
    {
        var originalIds = Provider.GetOrdersByType(OrderType.Bid).Concat(Provider.GetOrdersByType(OrderType.Ask))
            .Select(order => order.Id).ToArray();
        using var response = await Post(json);

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
        Assert.That(Provider.GetOrdersByType(OrderType.Bid).Concat(Provider.GetOrdersByType(OrderType.Ask))
            .Select(order => order.Id), Is.EquivalentTo(originalIds));
    }

    [TestCase("/checks/missing", HttpStatusCode.NotFound)]
    [TestCase("/checks/unexpected", HttpStatusCode.InternalServerError)]
    public async Task ExceptionMiddlewareReturnsProblemDetailsWithoutLeakingPrivateDetails(
        string route, HttpStatusCode expectedStatus)
    {
        using var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(problem.RootElement.GetProperty("status").GetInt32(), Is.EqualTo((int)expectedStatus));
        Assert.That(body, Does.Not.Contain("Private failure details"));
    }

    private async Task<Guid> CreateBid()
    {
        using var response = await Post("""{"trader_id":1,"type":"Bid","stock_id":1,"price":9,"amount":3}""");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> Get(string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> Post(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync("/orders", content);
    }

    private static IResult MissingResource() => throw new KeyNotFoundException("Missing test resource.");
    private static IResult UnexpectedFailure() => throw new Exception("Private failure details");
}
