using System;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Enums;

namespace InfoDumpManager.Domain.Entities;

public class ActivityLog : Entity
{
    private ActivityLog() { }

    public ActivityType ActivityType { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? GemId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string Message { get; private set; } = string.Empty;

    public static ActivityLog Create(ActivityType activityType, string message, Guid? userId = null, Guid? gemId = null, Guid? categoryId = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be empty", nameof(message));
        }

        return new ActivityLog
        {
            ActivityType = activityType,
            Message = message.Trim(),
            UserId = userId,
            GemId = gemId,
            CategoryId = categoryId
        };
    }
}
