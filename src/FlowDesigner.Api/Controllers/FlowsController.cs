using FlowDesigner.Api.Services;
using FlowDesigner.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesigner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlowsController : ControllerBase
{
    private readonly FlowService _flowService;

    public FlowsController(FlowService flowService)
    {
        _flowService = flowService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Flow>>> GetAll()
    {
        return await _flowService.GetAllFlowsAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Flow>> Get(string id)
    {
        var flow = await _flowService.GetFlowAsync(id);
        if (flow == null)
            return NotFound();
        return flow;
    }

    [HttpPost]
    public async Task<ActionResult<Flow>> Create([FromBody] Flow flow)
    {
        var created = await _flowService.CreateFlowAsync(flow);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Flow>> Update(string id, [FromBody] Flow flow)
    {
        var updated = await _flowService.UpdateFlowAsync(id, flow);
        if (updated == null)
            return NotFound();
        return updated;
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var success = await _flowService.DeleteFlowAsync(id);
        if (!success)
            return NotFound();
        return NoContent();
    }
}
