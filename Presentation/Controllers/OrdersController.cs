using BL.Abstractions;
using Common.Requests;
using Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(IOrderBookingService ordersBooking) : ControllerBase
{
    [HttpPost]
    public IActionResult CreateOrder(CreateOrderRequest request)
    {
        var order = ordersBooking.CreateOrder(new Order
        {
            TraderId = request.TraderId, Type = request.Type, StockId = request.StockId,
            Price = request.Price, Amount = request.Amount
        });
        return Ok(new { order.Id });
    }

    [HttpDelete("{id:guid}")]
    public IActionResult CancelOrder(Guid id)
    {
        ordersBooking.CancelOrder(id);
        return NoContent();
    }
}