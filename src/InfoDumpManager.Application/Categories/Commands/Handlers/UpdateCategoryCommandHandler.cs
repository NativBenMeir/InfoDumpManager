using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Categories.Dtos;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands.Handlers;

public sealed class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto?>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<CategoryDto?> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            category.Rename(request.Name);
        }

        if (request.Description is not null)
        {
            category.UpdateDescription(request.Description);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<CategoryDto>(category);
    }
}
