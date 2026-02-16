using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.DTOs;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Application.GEMs.Queries;
using InfoDumpManager.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InfoDumpManager.Web.Pages.GEMs;

public sealed class DetailModel : PageModel
{
private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUserContext _currentUserContext;

    public DetailModel(
        IMediator mediator,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentUserContext currentUserContext)
    {
        _mediator = mediator;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUserContext = currentUserContext;
    }

    public GEMDto? Gem { get; private set; }

    public IReadOnlyCollection<CategoryDto> Categories { get; private set; } = Array.Empty<CategoryDto>();

    [BindProperty]
    public Guid? SelectedCategoryId { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetGEMByIdQuery { GemId = id };
        Gem = await _mediator.Send(query, cancellationToken);
        if (Gem is null)
        {
            return NotFound();
        }

        await LoadCategoriesAsync(cancellationToken);
        SelectedCategoryId = Gem.CategoryId;

        return Page();
    }

    public async Task<IActionResult> OnPostAssignCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!SelectedCategoryId.HasValue)
        {
            ModelState.AddModelError(nameof(SelectedCategoryId), "Please select a category.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateModelAsync(id, cancellationToken);
            return Page();
        }

        var command = new AssignCategoryCommand
        {
            GemId = id,
            CategoryId = SelectedCategoryId!.Value
        };

        await _mediator.Send(command, cancellationToken);
        StatusMessage = "Category assignment updated.";

        return RedirectToPage("/GEMs/Detail", new { id });
    }

    private async Task PopulateModelAsync(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetGEMByIdQuery { GemId = id };
        Gem = await _mediator.Send(query, cancellationToken);
        await LoadCategoriesAsync(cancellationToken);
        SelectedCategoryId = Gem?.CategoryId;
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await _unitOfWork.Categories.ListByTenantAsync(_currentUserContext.TenantId, cancellationToken);
        Categories = categories.Select(category => _mapper.Map<CategoryDto>(category)).ToList();
    }
}
