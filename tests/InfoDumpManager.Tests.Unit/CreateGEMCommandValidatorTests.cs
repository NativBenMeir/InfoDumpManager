using FluentAssertions;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.Validators;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class CreateGEMCommandValidatorTests
{
    private readonly CreateGEMCommandValidator _validator = new();

    [Fact]
    public void Should_Flag_InvalidUrl()
    {
        var command = new CreateGEMCommand
        {
            Title = "Integration GEM",
            Url = "invalid-url",
            SourceUrl = "https://source.example.com",
            SnapshotHtml = "<html></html>"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Url");
    }
}
