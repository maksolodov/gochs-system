using Microsoft.AspNetCore.Mvc;
using Gochs.Modules.Wartime.DTOs;
using Gochs.Modules.Wartime.Models;
using Gochs.Modules.Wartime.Services;

namespace Gochs.Modules.Wartime.Controllers;

[ApiController]
[Route("api/wartime/orders")]
public class WartimeOrdersController : ControllerBase
{
    private readonly IWartimeOrderService _service;

    public WartimeOrdersController(IWartimeOrderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WartimeTransitionOrder>>> GetOrders()
    {
        return Ok(await _service.GetOrdersAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WartimeTransitionOrder>> GetOrder(int id)
    {
        var order = await _service.GetOrderAsync(id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<WartimeTransitionOrder>> CreateOrder(WartimeOrderUpsertDto dto)
    {
        try
        {
            var order = await _service.CreateOrderAsync(dto);
            return Created($"/api/wartime/orders/{order.Id}", order);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<WartimeTransitionOrder>> UpdateOrder(int id, WartimeOrderUpsertDto dto)
    {
        try
        {
            var order = await _service.UpdateOrderAsync(id, dto);
            return order is null ? NotFound() : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/sign")]
    public async Task<ActionResult<WartimeTransitionOrder>> SignOrder(int id, SignWartimeOrderDto dto)
    {
        try
        {
            var order = await _service.SignOrderAsync(id, dto);
            return order is null ? NotFound() : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/activate")]
    public async Task<ActionResult<WartimeTransitionOrder>> ActivateOrder(int id)
    {
        try
        {
            var order = await _service.ActivateOrderAsync(id);
            return order is null ? NotFound() : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/document")]
    public async Task<IActionResult> GetDocument(int id)
    {
        var document = await _service.GetDocumentHtmlAsync(id);
        return document is null
            ? NotFound()
            : Content(document, "text/html; charset=utf-8");
    }

    [HttpGet("{id:int}/audit")]
    public async Task<ActionResult<IEnumerable<WartimeActionLog>>> GetAudit(int id)
    {
        return Ok(await _service.GetAuditAsync(id));
    }

    [HttpGet("state")]
    public async Task<ActionResult<WartimeOrderStateDto>> GetState()
    {
        return Ok(await _service.GetStateAsync());
    }
}
