using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.GEMs.Queries;
using InfoDumpManager.WebAPI.Contracts.GEMs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfoDumpManager.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "MultiTenant")]
[Route("api/v1/gems")]
public sealed class GEMsController : ControllerBase
{
    private readonly IMediator _mediator;
    public GEMsController(
        IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGemRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateGEMCommand
        {
            Title = request.Title,
            Url = request.Url,
            SourceUrl = request.SourceUrl,
            SourceTitle = request.SourceTitle,
            SnapshotHtml = request.SnapshotHtml,
            SnapshotMimeType = request.SnapshotMimeType,
            SnapshotCapturedAt = request.SnapshotCapturedAt,
            SummaryText = request.SummaryText,
            SummaryModel = request.SummaryModel,
            SummaryTokenCount = request.SummaryTokenCount,
            SummaryGeneratedAt = request.SummaryGeneratedAt,
            OnDuplicate = (InfoDumpManager.Application.GEMs.Commands.CreateGEMOnDuplicateMode)request.OnDuplicate
        };

        var result = await _mediator.Send(command, cancellationToken);
        var response = new CreateGemResponse
        {
            Outcome = (Contracts.GEMs.CreateGemOutcome)result.Outcome,
            Gem = result.Gem,
            ExistingGemId = result.ExistingGemId,
            Message = result.Message
        };

        return result.Outcome switch
        {
            InfoDumpManager.Application.GEMs.Commands.CreateGEMOutcome.Created => CreatedAtAction(nameof(GetById), new { id = result.Gem!.Id }, response),
            InfoDumpManager.Application.GEMs.Commands.CreateGEMOutcome.DuplicateFound => Conflict(response),
            InfoDumpManager.Application.GEMs.Commands.CreateGEMOutcome.UpdatedExisting => Ok(response),
            InfoDumpManager.Application.GEMs.Commands.CreateGEMOutcome.CreatedNewVersion => CreatedAtAction(nameof(GetById), new { id = result.Gem!.Id }, response),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var query = new GetGEMByIdQuery { GemId = id };
        var gem = await _mediator.Send(query, cancellationToken);
        if (gem is null)
        {
            return NotFound();
        }

        return Ok(gem);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GemQueryParameters query, CancellationToken cancellationToken)
    {
        var listQuery = new ListGEMsQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };

        var result = await _mediator.Send(listQuery, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{gemId:guid}/category")]
    public async Task<IActionResult> AssignCategory([FromRoute] Guid gemId, [FromBody] AssignCategoryRequest request, CancellationToken cancellationToken)
    {
        var command = new AssignCategoryCommand
        {
            GemId = gemId,
            CategoryId = request.CategoryId
        };

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
