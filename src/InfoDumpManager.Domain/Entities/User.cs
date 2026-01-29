using System;
using Microsoft.AspNetCore.Identity;

namespace InfoDumpManager.Domain.Entities;

public sealed class User : IdentityUser<Guid>
{
    public Guid TenantId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public byte[]? RowVersion { get; private set; }

    private User()
    {
    }

    public static User Create(Guid tenantId, string userName, string email, string displayName)
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

        var normalizedUserName = userName.Trim();
        var normalizedEmail = email.Trim();

        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = normalizedUserName,
            NormalizedUserName = normalizedUserName.ToUpperInvariant(),
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail.ToUpperInvariant(),
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

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
    }

    public void RecordActivity()
    {
        LastSeenAt = DateTimeOffset.UtcNow;
    }
}
