using System;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Common;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Application.GEMs.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfoDumpManager.WebAPI.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class GemsController : ControllerBase
{
    private readonly ISender _sender;

    public GemsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GemDto>> GetGemById(Guid id, CancellationToken ct = default)
    {
        var gem = await _sender.Send(new GetGemByIdQuery(id), ct);
        if (gem is null)
        {
            return NotFound(new { message = "GEM not found" });
        }

        return Ok(gem);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<GemDto>>> GetGems([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new { message = "Invalid pagination parameters" });
        }

        var result = await _sender.Send(new GetGemsQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<GemDto>> CreateGem(CreateGEMRequest request, CancellationToken ct = default)
    {
        var gem = await _sender.Send(new CreateGemCommand(request.Url, request.Title), ct);
        return CreatedAtAction(nameof(GetGemById), new { id = gem.Id }, gem);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GemDto>> UpdateGem(Guid id, UpdateGEMRequest request, CancellationToken ct = default)
    {
        var gem = await _sender.Send(new UpdateGemCommand(id, request.Title), ct);
        if (gem is null)
        {
            return NotFound(new { message = "GEM not found" });
        }

        return Ok(gem);
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteGem(Guid id) => NoContent();
}

public sealed class CreateGEMRequest
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public sealed class UpdateGEMRequest
{
    public string? Title { get; set; }
}
