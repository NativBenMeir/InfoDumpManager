using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Categories.DTOs;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InfoDumpManager.Web.Pages.Categories;

public sealed class ManageModel : PageModel
{
    private const int MaxNameLength = 128;
    private const int MaxDescriptionLength = 512;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDatabasePolicy _databasePolicy;
    private readonly IMediator _mediator;

    public ManageModel(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentUserContext currentUserContext,
        IDatabasePolicy databasePolicy,
        IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUserContext = currentUserContext;
        _databasePolicy = databasePolicy;
        _mediator = mediator;
    }

    public IReadOnlyCollection<CategoryDto> Categories { get; private set; } = Array.Empty<CategoryDto>();

    [BindProperty]
    public CreateCategoryInput Create { get; set; } = new();

    [BindProperty]
    public EditCategoryInput Edit { get; set; } = new();

    [BindProperty]
    public Guid DeleteCategoryId { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadCategoriesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(Create, nameof(Create)))
        {
            await LoadCategoriesAsync(cancellationToken);
            return Page();
        }

        var command = new CreateCategoryCommand
        {
            Name = Create.Name,
            Description = Create.Description
        };

        await _mediator.Send(command, cancellationToken);
        StatusMessage = "Category created.";

        return RedirectToPage("/Categories/Manage");
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(Edit, nameof(Edit)))
        {
            await LoadCategoriesAsync(cancellationToken);
            return Page();
        }

        var category = await _unitOfWork.Categories.GetByIdAsync(Edit.CategoryId, cancellationToken);
        if (category is null || category.TenantId != _currentUserContext.TenantId)
        {
            return NotFound();
        }

        if (!string.Equals(category.Name, Edit.Name, StringComparison.Ordinal))
        {
            category.UpdateName(Edit.Name);
        }

        category.UpdateDescription(Edit.Description);

        await _databasePolicy.ExecuteAsync(() => _unitOfWork.SaveChangesAsync(cancellationToken), cancellationToken);
        StatusMessage = "Category updated.";

        return RedirectToPage("/Categories/Manage");
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(DeleteCategoryId, cancellationToken);
        if (category is null || category.TenantId != _currentUserContext.TenantId)
        {
            return NotFound();
        }

        await _unitOfWork.Categories.RemoveAsync(category, cancellationToken);
        await _databasePolicy.ExecuteAsync(() => _unitOfWork.SaveChangesAsync(cancellationToken), cancellationToken);
        StatusMessage = "Category deleted.";

        return RedirectToPage("/Categories/Manage");
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await _unitOfWork.Categories.ListByTenantAsync(_currentUserContext.TenantId, cancellationToken);
        Categories = categories
            .OrderBy(category => category.Name)
            .Select(category => _mapper.Map<CategoryDto>(category))
            .ToList();
    }

    public sealed class CreateCategoryInput
    {
        [Required]
        [StringLength(MaxNameLength)]
        [Display(Name = "Category name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(MaxDescriptionLength)]
        [Display(Name = "Description")]
        public string? Description { get; set; }
    }

    public sealed class EditCategoryInput
    {
        public Guid CategoryId { get; set; }

        [Required]
        [StringLength(MaxNameLength)]
        [Display(Name = "Category name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(MaxDescriptionLength)]
        [Display(Name = "Description")]
        public string? Description { get; set; }
    }
}
