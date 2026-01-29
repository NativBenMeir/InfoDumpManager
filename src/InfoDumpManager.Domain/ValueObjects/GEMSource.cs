using System;
using System.Collections.Generic;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.ValueObjects;

public sealed class GEMSource : ValueObject
{
    public string Url { get; private set; } = string.Empty;
    public string? Title { get; private set; }

    private GEMSource()
    {
    }

    public GEMSource(string url, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Source URL is required.", nameof(url));
        }

        if (!Uri.IsWellFormedUriString(url.Trim(), UriKind.Absolute))
        {
            throw new ArgumentException("Source URL must be absolute.", nameof(url));
        }

        Url = url.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Url;
        yield return Title;
    }
}
