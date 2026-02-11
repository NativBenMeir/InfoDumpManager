using System;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

/// <summary>
/// Domain representation of a user. Does not depend on ASP.NET Identity.
/// </summary>
public sealed class UserProfile : AggregateRoot<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }

    private UserProfile()
    {
    }

    public static UserProfile Create(Guid tenantId, string userName, string email, string displayName)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException("Username is required.", nameof(userName));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        return new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = userName.Trim(),
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        DisplayName = displayName.Trim();
    }

    public void SetActiveStatus(bool isActive) => IsActive = isActive;

    public void RecordActivity() => LastSeenAt = DateTimeOffset.UtcNow;
}
