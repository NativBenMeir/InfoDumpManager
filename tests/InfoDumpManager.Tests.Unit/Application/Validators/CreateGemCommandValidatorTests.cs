using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.Validators;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Validators;

public class CreateGemCommandValidatorTests
{
    [Fact]
    public void Validate_ValidCommand_IsSuccessful()
    {
        var validator = new CreateGemCommandValidator();
        var command = new CreateGemCommand("https://example.com", "Title");

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidUrl_ReturnsValidationFailure()
    {
        var validator = new CreateGemCommandValidator();
        var command = new CreateGemCommand("not-a-url", "Title");

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Url");
    }

    [Fact]
    public void Validate_EmptyTitle_ReturnsValidationFailure()
    {
        var validator = new CreateGemCommandValidator();
        var command = new CreateGemCommand("https://example.com", "");

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Title");
    }
}
