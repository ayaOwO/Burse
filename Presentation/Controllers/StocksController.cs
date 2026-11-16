using Common.Models;
using BL.Services;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("stocks")]
public class StocksController(StockService stocks) : ControllerBase
{
    [HttpGet]
    public ActionResult<IList<StockInfo>> GetStocks()
    {
        return Ok(stocks.GetStocks());
    }

    [HttpGet("{id:int}")]
    public ActionResult<StockDetails> GetStock(int id)
    {
        return Ok(stocks.GetStock(id));
    }
}
