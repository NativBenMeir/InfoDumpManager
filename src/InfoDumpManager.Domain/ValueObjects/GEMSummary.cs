using System;

namespace InfoDumpManager.Domain.ValueObjects;

public sealed record GEMSummary(string Text, DateTime GeneratedAtUtc)
{
    public static GEMSummary Create(string text, DateTime? generatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Summary cannot be empty", nameof(text));
        }

        return new GEMSummary(text, generatedAtUtc ?? DateTime.UtcNow);
    }
}
