using BL.Services;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(OrderBookingService ordersBooking) : ControllerBase
{
    [HttpPost]
    public IActionResult CreateOrder(CreateOrderRequest request)
    {
        var order = ordersBooking.CreateOrder(request.TraderId, request.Type, request.StockId, request.Price, request.Amount);
        return Ok(new { order.Id });
    }

    [HttpDelete("{id:guid}")]
    public IActionResult CancelOrder(Guid id)
    {
        ordersBooking.CancelOrder(id);
        return NoContent();
    }
}
