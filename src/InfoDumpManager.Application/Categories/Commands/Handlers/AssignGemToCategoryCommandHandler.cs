using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands.Handlers;

public sealed class AssignGemToCategoryCommandHandler : IRequestHandler<AssignGemToCategoryCommand, bool>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignGemToCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AssignGemToCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return false;
        }

        category.AssignGem(request.GemId);
        await _categoryRepository.UpdateAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
