using BL.Abstractions;
using BL.Options;
using BL.OrderBookMatchers;
using BL.Services;
using BL.StockUpdaters;
using DAL;
using DAL.Options;
using Serilog;
using Presentation.Filters;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("BurseJson.json", optional: false);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSwaggerGen();

builder.Services.Configure<MarketDataProviderOptions>(builder.Configuration);
builder.Services.AddSingleton<IMarketDataProvider, MemoryMarketDataProvider>();

builder.Services.Configure<RandomStockUpdaterOptions>(builder.Configuration.GetSection(nameof(RandomStockUpdaterOptions)));
builder.Services.AddSingleton<IStockUpdater, RandomStockUpdater>();

builder.Services.Configure<StockUpdaterServiceOptions>(builder.Configuration.GetSection(nameof(StockUpdaterServiceOptions)));
builder.Services.AddHostedService<StockUpdaterService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// app.UseAuthorization();

app.MapControllers();
app.Run();
