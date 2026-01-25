using System;
using FluentValidation;
using InfoDumpManager.Application.GEMs.Commands;

namespace InfoDumpManager.Application.Validators;

public sealed class UpdateGemCommandValidator : AbstractValidator<UpdateGemCommand>
{
    public UpdateGemCommandValidator()
    {
        RuleFor(x => x.GemId)
            .NotEqual(Guid.Empty)
            .WithMessage("A valid GEM identifier is required.");

        RuleFor(x => x.Title)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Title));
    }
}
