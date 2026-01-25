using System;
using FluentAssertions;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Validators;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Validators;

public class RemoveGemFromCategoryCommandValidatorTests
{
    [Fact]
    public void Validate_ValidCommand_IsSuccessful()
    {
        var validator = new RemoveGemFromCategoryCommandValidator();
        var command = new RemoveGemFromCategoryCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidCategoryId_ReturnsValidationFailure()
    {
        var validator = new RemoveGemFromCategoryCommandValidator();
        var command = new RemoveGemFromCategoryCommand(Guid.Empty, Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "CategoryId");
    }

    [Fact]
    public void Validate_InvalidGemId_ReturnsValidationFailure()
    {
        var validator = new RemoveGemFromCategoryCommandValidator();
        var command = new RemoveGemFromCategoryCommand(Guid.NewGuid(), Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "GemId");
    }
}
