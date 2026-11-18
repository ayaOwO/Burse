using Common;
using DAL;

namespace Presentation;

public class Bootstrap
{
    public static IDictionary<int, SemaphoreSlim> CreateSemaphores(IServiceProvider services, object key)
    {
        var provider = services.GetRequiredService<IMarketDataProvider>();
        IEnumerable<int> ids;
        switch (key)
        {
            case ServiceKeys.StockSemaphores:
            {
                ids = provider.GetAllStocks().Select(stock => stock.Id);
                break;
            }
            default:
            {
                ids = provider.GetAllTraders().Select(trader => trader.Id);
                break;
            }
        }

        return ids.ToDictionary(id => id, id => new SemaphoreSlim(1, 1));
    }
}