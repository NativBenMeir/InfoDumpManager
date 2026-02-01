using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.DTOs;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InfoDumpManager.Web.Pages.GEMs;

public sealed class ListModel : PageModel
{
    private const int MaxPageSize = 100;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUserContext _currentUserContext;

    public ListModel(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUserContext currentUserContext)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUserContext = currentUserContext;
    }

    public IReadOnlyCollection<GEMDto> Gems { get; private set; } = Array.Empty<GEMDto>();

    public IReadOnlyCollection<CategoryDto> Categories { get; private set; } = Array.Empty<CategoryDto>();

    public int PageNumber { get; private set; }

    public int PageSize { get; private set; }

    public int Total { get; private set; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    [BindProperty(SupportsGet = true)]
    public Guid? CategoryId { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumberQuery { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "pageSize")]
    public int PageSizeQuery { get; set; } = 20;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PageNumber = Math.Max(1, PageNumberQuery);
        PageSize = Math.Clamp(PageSizeQuery, 1, MaxPageSize);

        var categories = await _unitOfWork.Categories.ListByTenantAsync(_currentUserContext.TenantId, cancellationToken);
        Categories = categories.Select(category => _mapper.Map<CategoryDto>(category)).ToList();

        var gems = await LoadGemsAsync(cancellationToken);
        Total = gems.Count;

        Gems = gems
            .OrderByDescending(gem => gem.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(gem => _mapper.Map<GEMDto>(gem))
            .ToList();
    }

    private async Task<IReadOnlyCollection<InfoDumpManager.Domain.Entities.GEM>> LoadGemsAsync(CancellationToken cancellationToken)
    {
        if (!CategoryId.HasValue)
        {
            return await _unitOfWork.GEMs.ListByTenantAsync(_currentUserContext.TenantId, cancellationToken);
        }

        var category = await _unitOfWork.Categories.GetByIdAsync(CategoryId.Value, cancellationToken);
        if (category is null || category.TenantId != _currentUserContext.TenantId)
        {
            return Array.Empty<InfoDumpManager.Domain.Entities.GEM>();
        }

        return await _unitOfWork.GEMs.ListByCategoryAsync(CategoryId.Value, cancellationToken);
    }
}
