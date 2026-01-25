using System;
using System.Collections.Generic;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

public class Category : Entity, IAggregateRoot
{
    private List<Guid> _gemIds = new();

    private Category() { }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IReadOnlyCollection<Guid> GemIds => _gemIds.AsReadOnly();

    // EF Core backing field for JSON serialization
    public string? _gemIdsJson { get; private set; }

    public static Category Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty", nameof(name));
        }

        return new Category
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty", nameof(name));
        }

        Name = name.Trim();
        Touch();
    }

    public void UpdateDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Touch();
    }

    public void AssignGem(Guid gemId)
    {
        if (!_gemIds.Contains(gemId))
        {
            _gemIds.Add(gemId);
            Touch();
        }
    }

    public void RemoveGem(Guid gemId)
    {
        if (_gemIds.Remove(gemId))
        {
            Touch();
        }
    }
}
