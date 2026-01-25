using System;
using FluentValidation;
using InfoDumpManager.Application.Categories.Commands;

namespace InfoDumpManager.Application.Validators;

public sealed class AssignGemToCategoryCommandValidator : AbstractValidator<AssignGemToCategoryCommand>
{
    public AssignGemToCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("Category identifier is required.");

        RuleFor(x => x.GemId)
            .NotEqual(Guid.Empty)
            .WithMessage("GEM identifier is required.");
    }
}
