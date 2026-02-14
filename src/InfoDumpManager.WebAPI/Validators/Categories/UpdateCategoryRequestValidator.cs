using FluentValidation;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.WebAPI.Contracts.Categories;

namespace InfoDumpManager.WebAPI.Validators.Categories;

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Category.MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(Category.MaxDescriptionLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
