using System;

namespace InfoDumpManager.Application.Services;

public sealed record PageSnapshot(string Content, string ContentType, DateTime RetrievedAtUtc);
