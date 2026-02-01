using System;
using FluentValidation;
using InfoDumpManager.WebAPI.Contracts.GEMs;

namespace InfoDumpManager.WebAPI.Validators.GEMs;

public sealed class AssignCategoryRequestValidator : AbstractValidator<AssignCategoryRequest>
{
    public AssignCategoryRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty()
            .WithMessage("Category selection is required.");
    }
}
