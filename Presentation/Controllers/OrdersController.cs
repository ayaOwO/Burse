using BL.Services;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(OrderService orders) : ControllerBase
{
    [HttpPost]
    public IActionResult CreateOrder(CreateOrderRequest request)
    {
        var order = orders.CreateOrder(request.TraderId, request.Type, request.StockId, request.Price, request.Amount);
        return Ok(new { order.Id });
    }

    [HttpDelete("{id:guid}")]
    public IActionResult CancelOrder(Guid id)
    {
        orders.CancelOrder(id);
        return NoContent();
    }
}
