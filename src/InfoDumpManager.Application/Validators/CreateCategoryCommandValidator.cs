using FluentValidation;
using InfoDumpManager.Application.Categories.Commands;

namespace InfoDumpManager.Application.Validators;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    private const int MaxNameLength = 128;
    private const int MaxDescriptionLength = 512;

    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
