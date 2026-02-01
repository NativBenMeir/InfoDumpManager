using System;
using FluentValidation;
using InfoDumpManager.WebAPI.Contracts.GEMs;

namespace InfoDumpManager.WebAPI.Validators.GEMs;

public sealed class CreateGemRequestValidator : AbstractValidator<CreateGemRequest>
{
    private const int MaxTitleLength = 256;
    private const int MaxMimeTypeLength = 64;

    public CreateGemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(MaxTitleLength);

        RuleFor(x => x.Url)
            .NotEmpty()
            .Must(BeValidUri)
            .WithMessage("Url must be a valid absolute HTTP or HTTPS URI.");

        RuleFor(x => x.SourceUrl)
            .NotEmpty()
            .Must(BeValidUri)
            .WithMessage("Source URL must be a valid absolute HTTP or HTTPS URI.");

        RuleFor(x => x.SnapshotHtml)
            .NotEmpty();

        RuleFor(x => x.SnapshotMimeType)
            .NotEmpty()
            .MaximumLength(MaxMimeTypeLength);

        RuleFor(x => x.SummaryTokenCount)
            .GreaterThanOrEqualTo(0);

        When(HasSummary, () =>
        {
            RuleFor(x => x.SummaryText).NotEmpty();
            RuleFor(x => x.SummaryModel).NotEmpty();
            RuleFor(x => x.SummaryGeneratedAt)
                .GreaterThan(DateTimeOffset.MinValue)
                .WithMessage("Summary generation timestamp must be specified.");
        });
    }

    private static bool BeValidUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https";
    }

    private static bool HasSummary(CreateGemRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.SummaryText) || !string.IsNullOrWhiteSpace(request.SummaryModel);
    }
}
