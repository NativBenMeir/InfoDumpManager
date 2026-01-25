using System;
using FluentValidation;
using InfoDumpManager.Application.GEMs.Commands;

namespace InfoDumpManager.Application.Validators;

public sealed class CreateGemCommandValidator : AbstractValidator<CreateGemCommand>
{
    public CreateGemCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("A valid absolute URL is required.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(500);
    }
}
