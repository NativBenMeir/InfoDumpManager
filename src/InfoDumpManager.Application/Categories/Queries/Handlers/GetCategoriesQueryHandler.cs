using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.Dtos;
using InfoDumpManager.Application.Categories.Queries;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.Categories.Queries.Handlers;

public sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IMapper _mapper;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository, IMapper mapper)
    {
        _categoryRepository = categoryRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<CategoryDto>>(categories);
    }
}
