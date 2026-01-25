using System;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

public class User : Entity, IAggregateRoot
{
    private User() { }

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;

    public static User Create(string email, string displayName)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be empty", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));
        }

        return new User
        {
            Email = email.Trim(),
            DisplayName = displayName.Trim()
        };
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));
        }

        DisplayName = displayName.Trim();
        Touch();
    }
}
