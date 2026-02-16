using System;
using System.Collections.Generic;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.ValueObjects;

public sealed class GEMSnapshot : ValueObject
{
    public string HtmlContent { get; private set; } = string.Empty;
    public string? TextContent { get; private set; }
    public string MimeType { get; private set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; private set; }

    private GEMSnapshot()
    {
    }

    public GEMSnapshot(
        string htmlContent,
        string mimeType = "text/html",
        DateTimeOffset? capturedAt = null,
        string? textContent = null)
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
        TextContent = string.IsNullOrWhiteSpace(textContent) ? null : textContent.Trim();
        MimeType = mimeType;
        CapturedAt = capturedAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a defensive copy of the GEMSnapshot.
    /// </summary>
    public GEMSnapshot Copy() => new(HtmlContent, MimeType, CapturedAt, TextContent);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return HtmlContent;
        yield return TextContent;
        yield return MimeType;
        yield return CapturedAt;
    }
}
