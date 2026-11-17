using Common.Models;

namespace BL.Abstractions;

public interface IOrderBookingService
{
    public Order CreateOrder(Order order);
    public void CancelOrder(Guid id);
}
