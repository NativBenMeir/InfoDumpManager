using System;
using FluentAssertions;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.Validators;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Validators;

public class UpdateGemCommandValidatorTests
{
    [Fact]
    public void Validate_ValidCommand_IsSuccessful()
    {
        var validator = new UpdateGemCommandValidator();
        var command = new UpdateGemCommand(Guid.NewGuid(), "New Title");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidGemId_ReturnsValidationFailure()
    {
        var validator = new UpdateGemCommandValidator();
        var command = new UpdateGemCommand(Guid.Empty, "New Title");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "GemId");
    }

    [Fact]
    public void Validate_TitleTooLong_ReturnsValidationFailure()
    {
        var validator = new UpdateGemCommandValidator();
        var command = new UpdateGemCommand(Guid.NewGuid(), new string('A', 501));

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Title");
    }
}
