using System;

namespace InfoDumpManager.Domain.ValueObjects;

public sealed record GEMSnapshot(string Content, string ContentType, DateTime RetrievedAtUtc)
{
    public static GEMSnapshot Create(string content, string contentType, DateTime retrievedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be empty", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be empty", nameof(contentType));
        }

        return new GEMSnapshot(content, contentType, retrievedAtUtc);
    }
}
