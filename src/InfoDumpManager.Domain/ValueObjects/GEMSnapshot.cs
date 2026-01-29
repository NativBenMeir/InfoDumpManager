using System;
using System.Collections.Generic;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.ValueObjects;

public sealed class GEMSnapshot : ValueObject
{
    public string HtmlContent { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; private set; }

    private GEMSnapshot()
    {
    }

    public GEMSnapshot(string htmlContent, string mimeType = "text/html", DateTimeOffset? capturedAt = null)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException("Snapshot content cannot be empty.", nameof(htmlContent));
        }

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new ArgumentException("MIME type is required.", nameof(mimeType));
        }

        HtmlContent = htmlContent;
        MimeType = mimeType;
        CapturedAt = capturedAt ?? DateTimeOffset.UtcNow;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return HtmlContent;
        yield return MimeType;
        yield return CapturedAt;
    }
}
