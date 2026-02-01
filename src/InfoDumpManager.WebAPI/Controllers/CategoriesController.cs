using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Categories.DTOs;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.WebAPI.Contracts.Categories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfoDumpManager.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "MultiTenant")]
[Route("api/v1/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDatabasePolicy _databasePolicy;

    public CategoriesController(
        IMediator mediator,
        IMapper mapper,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDatabasePolicy databasePolicy)
    {
        _mediator = mediator;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _databasePolicy = databasePolicy;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        var command = new CreateCategoryCommand
        {
            Name = request.Name,
            Description = request.Description
        };

        var category = await _mediator.Send(command);
        return Created(string.Empty, category);
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var categories = await _unitOfWork.Categories.ListByTenantAsync(_currentUserContext.TenantId);
        var dtos = categories.Select(c => _mapper.Map<CategoryDto>(c)).ToList();
        return Ok(dtos);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        if (category is null || category.TenantId != _currentUserContext.TenantId)
        {
            return NotFound();
        }

        if (category.Name != request.Name)
        {
            category.UpdateName(request.Name);
        }

        category.UpdateDescription(request.Description);

        await _databasePolicy.ExecuteAsync(() => _unitOfWork.SaveChangesAsync(cancellationToken), cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        if (category is null || category.TenantId != _currentUserContext.TenantId)
        {
            return NotFound();
        }

        await _unitOfWork.Categories.RemoveAsync(category);
        await _databasePolicy.ExecuteAsync(() => _unitOfWork.SaveChangesAsync(cancellationToken), cancellationToken);

        return NoContent();
    }
}
