using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Domain.Repositories;

public interface ICategorySuggestionRepository
{
    Task AddAsync(CategorySuggestion suggestion, CancellationToken cancellationToken = default);
    Task<CategorySuggestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CategorySuggestion>> ListByGemAsync(Guid gemId, CancellationToken cancellationToken = default);
}
