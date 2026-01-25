using System;
using FluentAssertions;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Validators;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Validators;

public class UpdateCategoryCommandValidatorTests
{
    [Fact]
    public void Validate_ValidCommand_IsSuccessful()
    {
        var validator = new UpdateCategoryCommandValidator();
        var command = new UpdateCategoryCommand(Guid.NewGuid(), "Revised name", "Updated description");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_ReturnsValidationFailure()
    {
        var validator = new UpdateCategoryCommandValidator();
        var command = new UpdateCategoryCommand(Guid.NewGuid(), string.Empty, "Updated description");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Name");
    }

    [Fact]
    public void Validate_InvalidCategoryId_ReturnsValidationFailure()
    {
        var validator = new UpdateCategoryCommandValidator();
        var command = new UpdateCategoryCommand(Guid.Empty, "Name", "Description");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "CategoryId");
    }
}
