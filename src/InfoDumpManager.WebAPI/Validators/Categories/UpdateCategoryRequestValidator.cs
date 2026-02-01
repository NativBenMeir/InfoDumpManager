using FluentValidation;
using InfoDumpManager.WebAPI.Contracts.Categories;

namespace InfoDumpManager.WebAPI.Validators.Categories;

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    private const int MaxNameLength = 128;
    private const int MaxDescriptionLength = 512;

    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
