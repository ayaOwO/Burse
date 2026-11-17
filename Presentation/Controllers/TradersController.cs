using Common.Models;
using BL.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("traders")]
public class TradersController(ITraderService traders) : ControllerBase
{
    [HttpGet]
    public ActionResult<IList<TraderSummary>> GetTraders()
    {
        return Ok(traders.GetTraders());
    }

    [HttpGet("{id:int}")]
    public ActionResult<TraderDetails> GetTrader(int id)
    {
        return Ok(traders.GetTrader(id));
    }

    [HttpGet("trades/{id:int}")]
    public ActionResult<TraderTradesDetails> GetTrades(int id)
    {
        return Ok(traders.GetTrades(id));
    }
}
