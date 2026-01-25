using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands.Handlers;

public sealed class RemoveGemFromCategoryCommandHandler : IRequestHandler<RemoveGemFromCategoryCommand, bool>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveGemFromCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(RemoveGemFromCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return false;
        }

        category.RemoveGem(request.GemId);
        await _categoryRepository.UpdateAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
