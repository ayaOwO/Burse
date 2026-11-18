using Common.Models;

namespace BL.Abstractions;

public interface IOrderBookingService
{
    public Task CreateOrder(Order order);
    public void CancelOrder(Guid id);
}
