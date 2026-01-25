using System;
using System.Collections.Generic;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.ValueObjects;

namespace InfoDumpManager.Domain.Entities;

public class GEM : Entity, IAggregateRoot
{
    private List<Guid> _categoryIds = new();

    private GEM() { }

    public GEMSource Source { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public GEMSnapshot? Snapshot { get; private set; }
    public GEMSummary? Summary { get; private set; }
    public IReadOnlyCollection<Guid> CategoryIds => _categoryIds.AsReadOnly();

    // EF Core backing field for JSON serialization
    public string? _categoryIdsJson { get; private set; }

    public static GEM Create(GEMSource source, string title, GEMSnapshot? snapshot = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty", nameof(title));
        }

        return new GEM
        {
            Source = source,
            Title = title.Trim(),
            Snapshot = snapshot
        };
    }

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty", nameof(title));
        }

        Title = title.Trim();
        Touch();
    }

    public void AttachSnapshot(GEMSnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Touch();
    }

    public void SetSummary(GEMSummary summary)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        Touch();
    }

    public void AssignCategory(Guid categoryId)
    {
        if (!_categoryIds.Contains(categoryId))
        {
            _categoryIds.Add(categoryId);
            Touch();
        }
    }

    public void RemoveCategory(Guid categoryId)
    {
        if (_categoryIds.Remove(categoryId))
        {
            Touch();
        }
    }
}
