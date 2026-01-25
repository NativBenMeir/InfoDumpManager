using System;
using FluentValidation;
using InfoDumpManager.Application.Categories.Commands;

namespace InfoDumpManager.Application.Validators;

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("Category identifier is required.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Name cannot be empty when provided")
            .When(x => x.Name is not null, ApplyConditionTo.CurrentValidator)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Name), ApplyConditionTo.CurrentValidator);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
