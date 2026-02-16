using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InfoDumpManager.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IMediator _mediator;
    private readonly IWebScrapingService _webScrapingService;
    private readonly IHtmlContentExtractor _htmlContentExtractor;

    public IndexModel(
        ILogger<IndexModel> logger,
        IMediator mediator,
        IWebScrapingService webScrapingService,
        IHtmlContentExtractor htmlContentExtractor)
    {
        _logger = logger;
        _mediator = mediator;
        _webScrapingService = webScrapingService;
        _htmlContentExtractor = htmlContentExtractor;
    }

    [BindProperty]
    [Required]
    [Url]
    [Display(Name = "Source URL")]
    public string SourceUrl { get; set; } = string.Empty;

    public void OnGet()
    {

    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var scrape = await _webScrapingService.ScrapeAsync(SourceUrl, cancellationToken);

            string? extractedText = null;
            try
            {
                extractedText = _htmlContentExtractor.ExtractMainText(scrape.HtmlContent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract main text for URL {Url}", scrape.Url);
            }

            var title = string.IsNullOrWhiteSpace(scrape.Title) ? scrape.Url : scrape.Title;

            var command = new CreateGEMCommand
            {
                Title = title,
                Url = scrape.Url,
                SourceUrl = scrape.Url,
                SourceTitle = scrape.Title,
                SnapshotHtml = scrape.HtmlContent,
                SnapshotText = extractedText,
                SnapshotMimeType = scrape.MimeType,
                SnapshotCapturedAt = scrape.CapturedAt
            };

            var result = await _mediator.Send(command, cancellationToken);

            if (result.Outcome == CreateGEMOutcome.Created && result.Gem is not null)
            {
                return RedirectToPage("/GEMs/Detail", new { id = result.Gem.Id });
            }

            if (result.Outcome == CreateGEMOutcome.DuplicateFound)
            {
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? "A GEM for this URL already exists. You can update the existing GEM or create a new GEM version when those options are enabled."
                    : result.Message;

                ModelState.AddModelError(string.Empty, message);
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Unable to submit this GEM right now. Please try again.");
            return Page();
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Web scraping timed out for URL {Url}", SourceUrl);
            ModelState.AddModelError(string.Empty, "The source page took too long to respond. Please try again, or use a faster/reachable URL.");
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit GEM for URL {Url}", SourceUrl);
            ModelState.AddModelError(string.Empty, "Unable to submit this GEM right now. Please try again.");
            return Page();
        }
    }
}
