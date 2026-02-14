using FluentValidation;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Application.Validators;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Category.MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(Category.MaxDescriptionLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
