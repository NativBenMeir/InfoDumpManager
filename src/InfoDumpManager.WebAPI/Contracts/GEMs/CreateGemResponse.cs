using System;
using System.Text.Json.Serialization;
using InfoDumpManager.Application.GEMs.DTOs;

namespace InfoDumpManager.WebAPI.Contracts.GEMs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CreateGemOutcome
{
    Created = 0,
    DuplicateFound = 1,
    UpdatedExisting = 2,
    CreatedNewVersion = 3
}

public sealed class CreateGemResponse
{
    public CreateGemOutcome Outcome { get; set; }

    public GEMDto? Gem { get; set; }

    public Guid? ExistingGemId { get; set; }

    public string? Message { get; set; }
}
