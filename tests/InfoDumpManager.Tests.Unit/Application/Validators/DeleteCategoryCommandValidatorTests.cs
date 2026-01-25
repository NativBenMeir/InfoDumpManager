using System;
using FluentAssertions;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Validators;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Validators;

public class DeleteCategoryCommandValidatorTests
{
    [Fact]
    public void Validate_ValidCommand_IsSuccessful()
    {
        var validator = new DeleteCategoryCommandValidator();
        var command = new DeleteCategoryCommand(Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidCategoryId_ReturnsValidationFailure()
    {
        var validator = new DeleteCategoryCommandValidator();
        var command = new DeleteCategoryCommand(Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "CategoryId");
    }
}
