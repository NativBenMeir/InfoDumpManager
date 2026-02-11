using System;
using FluentValidation;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Application.Validators;

public sealed class CreateGEMCommandValidator : AbstractValidator<CreateGEMCommand>
{
    private const int MaxMimeTypeLength = 64;

    public CreateGEMCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(GEM.MaxTitleLength);

        RuleFor(x => x.Url)
            .NotEmpty()
            .MaximumLength(GEM.MaxUrlLength)
            .Must(BeValidUri)
            .WithMessage("Url must be a valid absolute HTTP or HTTPS URI.");

        RuleFor(x => x.SourceUrl)
            .NotEmpty()
            .MaximumLength(GEM.MaxUrlLength)
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

    private static bool BeValidUri(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https";
    }

    private static bool HasSummary(CreateGEMCommand command)
    {
        return !string.IsNullOrWhiteSpace(command.SummaryText) || !string.IsNullOrWhiteSpace(command.SummaryModel);
    }
}
