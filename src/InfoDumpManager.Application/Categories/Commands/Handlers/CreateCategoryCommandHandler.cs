using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Categories.Dtos;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands.Handlers;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = Category.Create(request.Name, request.Description);
        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<CategoryDto>(category);
    }
}
