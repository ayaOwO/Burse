using Common.Models;
using BL.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("stocks")]
public class StocksController(IStockService stocks) : ControllerBase
{
    [HttpGet]
    public ActionResult<IList<Stock>> GetStocks()
    {
        return Ok(stocks.ListStocks());
    }

    [HttpGet("{id:int}")]
    public ActionResult<StockSummary> GetStock(int id)
    {
        return Ok(stocks.GetStockSummary(id));
    }
}
