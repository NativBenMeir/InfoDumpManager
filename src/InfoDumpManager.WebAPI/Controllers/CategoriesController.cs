using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Categories.Dtos;
using InfoDumpManager.Application.Categories.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfoDumpManager.WebAPI.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class CategoriesController : ControllerBase
{
    private readonly ISender _sender;

    public CategoriesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken ct = default)
    {
        var categories = await _sender.Send(new GetCategoriesQuery(), ct);
        return Ok(categories);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategoryById(Guid id, CancellationToken ct = default)
    {
        var category = await _sender.Send(new GetCategoryByIdQuery(id), ct);
        if (category is null)
        {
            return NotFound(new { message = "Category not found" });
        }

        return Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _sender.Send(new CreateCategoryCommand(request.Name, request.Description), ct);
        return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _sender.Send(new UpdateCategoryCommand(id, request.Name, request.Description), ct);
        if (category is null)
        {
            return NotFound(new { message = "Category not found" });
        }

        return Ok(category);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct = default)
    {
        var deleted = await _sender.Send(new DeleteCategoryCommand(id), ct);
        if (!deleted)
        {
            return NotFound(new { message = "Category not found" });
        }

        return NoContent();
    }

    [HttpPost("{id}/gems/{gemId}")]
    public async Task<IActionResult> AssignGemToCategory(Guid id, Guid gemId, CancellationToken ct = default)
    {
        var assigned = await _sender.Send(new AssignGemToCategoryCommand(id, gemId), ct);
        if (!assigned)
        {
            return NotFound(new { message = "Category not found" });
        }

        return NoContent();
    }

    [HttpDelete("{id}/gems/{gemId}")]
    public async Task<IActionResult> RemoveGemFromCategory(Guid id, Guid gemId, CancellationToken ct = default)
    {
        var removed = await _sender.Send(new RemoveGemFromCategoryCommand(id, gemId), ct);
        if (!removed)
        {
            return NotFound(new { message = "Category not found" });
        }

        return NoContent();
    }
}

public sealed class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class UpdateCategoryRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}
