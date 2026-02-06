using System;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

public sealed class CategorySuggestion : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxProposedNameLength = 128;
    private const int MaxRationaleLength = 2048;

    public Guid TenantId { get; private set; }
    public Guid GEMId { get; private set; }
    public Guid? SuggestedCategoryId { get; private set; }
    public string? ProposedCategoryName { get; private set; }
    public double ConfidenceScore { get; private set; }
    public string? Rationale { get; private set; }
    public bool AutoAssigned { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private CategorySuggestion()
    {
    }

    public static CategorySuggestion Create(
        Guid tenantId,
        Guid gemId,
        Guid? suggestedCategoryId,
        string? proposedCategoryName,
        double confidenceScore,
        string? rationale,
        bool autoAssigned)
    {
        ValidateTenant(tenantId);
        ValidateGuid(gemId, nameof(gemId));

        if (confidenceScore < 0 || confidenceScore > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence score must be between 0 and 1.");
        }

        var normalizedName = NormalizeName(proposedCategoryName);
        var normalizedRationale = NormalizeRationale(rationale);

        return new CategorySuggestion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GEMId = gemId,
            SuggestedCategoryId = suggestedCategoryId,
            ProposedCategoryName = normalizedName,
            ConfidenceScore = confidenceScore,
            Rationale = normalizedRationale,
            AutoAssigned = autoAssigned,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkAutoAssigned(bool autoAssigned)
    {
        AutoAssigned = autoAssigned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        }
    }

    private static void ValidateGuid(Guid value, string paramName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must be provided.", paramName);
        }
    }

    private static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxProposedNameLength)
        {
            throw new ArgumentException($"Proposed category name cannot exceed {MaxProposedNameLength} characters.", nameof(name));
        }

        return trimmed;
    }

    private static string? NormalizeRationale(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return null;
        }

        var trimmed = rationale.Trim();
        if (trimmed.Length > MaxRationaleLength)
        {
            throw new ArgumentException($"Rationale cannot exceed {MaxRationaleLength} characters.", nameof(rationale));
        }

        return trimmed;
    }
}
