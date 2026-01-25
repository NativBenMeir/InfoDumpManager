using FluentAssertions;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Validators;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Validators;

public class CreateCategoryCommandValidatorTests
{
    [Fact]
    public void Validate_ValidCommand_IsSuccessful()
    {
        var validator = new CreateCategoryCommandValidator();
        var command = new CreateCategoryCommand("Knowledge", "Test description");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_ReturnsValidationFailure()
    {
        var validator = new CreateCategoryCommandValidator();
        var command = new CreateCategoryCommand(string.Empty, "Test");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WhitespaceName_ReturnsValidationFailure()
    {
        var validator = new CreateCategoryCommandValidator();
        var command = new CreateCategoryCommand("   ", "Test");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Name");
    }
}
