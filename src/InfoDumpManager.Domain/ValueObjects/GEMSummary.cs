using System;
using System.Collections.Generic;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.ValueObjects;

public sealed class GEMSummary : ValueObject
{
    private GEMSummary()
    {
    }

    private GEMSummary(string text, string model, int tokenCount, DateTimeOffset generatedAt)
    {
        Text = text;
        Model = model;
        TokenCount = tokenCount;
        GeneratedAt = generatedAt;
    }

    public string Text { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int TokenCount { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }

    public static GEMSummary Empty => new("", string.Empty, 0, DateTimeOffset.MinValue);

    public static GEMSummary Create(string text, string model, int tokenCount, DateTimeOffset generatedAt)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Summary text is required.", nameof(text));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model name is required.", nameof(model));
        }

        if (tokenCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count cannot be negative.");
        }

        return new GEMSummary(text.Trim(), model.Trim(), tokenCount, generatedAt);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Text;
        yield return Model;
        yield return TokenCount;
        yield return GeneratedAt;
    }
}
