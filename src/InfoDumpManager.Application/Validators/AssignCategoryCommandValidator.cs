using System;
using FluentValidation;
using InfoDumpManager.Application.GEMs.Commands;

namespace InfoDumpManager.Application.Validators;

public sealed class AssignCategoryCommandValidator : AbstractValidator<AssignCategoryCommand>
{
    public AssignCategoryCommandValidator()
    {
        RuleFor(x => x.GemId)
            .NotEmpty()
            .WithMessage("GEM identifier is required.");

        RuleFor(x => x.CategoryId)
            .NotEmpty()
            .WithMessage("Category identifier is required.");
    }
}
