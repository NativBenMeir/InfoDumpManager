using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.Dtos;
using InfoDumpManager.Application.Categories.Queries;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.Categories.Queries.Handlers;

public sealed class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, CategoryDto?>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IMapper _mapper;

    public GetCategoryByIdQueryHandler(ICategoryRepository categoryRepository, IMapper mapper)
    {
        _categoryRepository = categoryRepository;
        _mapper = mapper;
    }

    public async Task<CategoryDto?> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        return category is null ? null : _mapper.Map<CategoryDto>(category);
    }
}
