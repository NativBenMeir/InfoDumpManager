using System;
using FluentAssertions;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.Validators;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class AssignCategoryCommandValidatorTests
{
    private readonly AssignCategoryCommandValidator _validator = new();

    [Fact]
    public void Should_Flag_MissingGemId()
    {
        var command = new AssignCategoryCommand
        {
            GemId = Guid.Empty,
            CategoryId = Guid.NewGuid()
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "GemId");
    }

    [Fact]
    public void Should_Flag_MissingCategoryId()
    {
        var command = new AssignCategoryCommand
        {
            GemId = Guid.NewGuid(),
            CategoryId = Guid.Empty
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "CategoryId");
    }
}
