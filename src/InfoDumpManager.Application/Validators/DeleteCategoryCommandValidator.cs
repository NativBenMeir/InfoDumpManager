using System;
using FluentValidation;
using InfoDumpManager.Application.Categories.Commands;

namespace InfoDumpManager.Application.Validators;

public sealed class DeleteCategoryCommandValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("Category identifier is required.");
    }
}
