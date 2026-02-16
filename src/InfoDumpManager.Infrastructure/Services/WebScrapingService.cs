using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Polly;

namespace InfoDumpManager.Infrastructure.Services;

public interface IWebScrapingService
{
    Task<WebScrapeResult> ScrapeAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class WebScrapingService : IWebScrapingService
{
    private readonly ILogger<WebScrapingService> _logger;
    private readonly IAsyncPolicy _policy;
    private readonly WebScrapingOptions _options;

    public WebScrapingService(IOptions<WebScrapingOptions> options, ILogger<WebScrapingService> logger)
    {
        _options = options?.Value ?? new WebScrapingOptions();
        _logger = logger;

        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: _options.RetryCount,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * _options.RetryBaseDelayMs),
                onRetry: (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(exception, "Retrying web scrape attempt {RetryCount} after {DelayMs}ms.", retryCount, timeSpan.TotalMilliseconds);
                });

        var breakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: _options.CircuitBreakerFailures,
                durationOfBreak: TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                onBreak: (exception, duration) =>
                {
                    _logger.LogError(exception, "Web scraping circuit breaker opened for {DurationSeconds}s.", duration.TotalSeconds);
                },
                onReset: () => _logger.LogInformation("Web scraping circuit breaker reset."));

        _policy = Policy.WrapAsync(retryPolicy, breakerPolicy);
    }

    public Task<WebScrapeResult> ScrapeAsync(string url, CancellationToken cancellationToken = default)
    {
        var normalizedUrl = WebScrapingUtilities.NormalizeUrl(url);
        return _policy.ExecuteAsync(token => ScrapeInternalAsync(normalizedUrl, token), cancellationToken);
    }

    private async Task<WebScrapeResult> ScrapeInternalAsync(string normalizedUrl, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scraping URL {Url}", normalizedUrl);

        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        }).ConfigureAwait(false);

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = false
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);
        var timeoutMs = Math.Max(_options.TimeoutSeconds, 1) * 1000;

        try
        {
            IResponse? response;
            try
            {
                response = await page.GotoAsync(
                    normalizedUrl,
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = timeoutMs
                    }).ConfigureAwait(false);

                // Some modern pages keep background network activity open indefinitely
                // (analytics/beacons/long-polling). Avoid failing a successful navigation
                // purely because network never becomes fully idle.
                try
                {
                    await page.WaitForLoadStateAsync(
                        LoadState.NetworkIdle,
                        new PageWaitForLoadStateOptions
                        {
                            Timeout = Math.Max(1000, timeoutMs / 3)
                        }).ConfigureAwait(false);
                }
                catch (PlaywrightException ex) when (IsTimeoutException(ex))
                {
                    _logger.LogDebug(
                        ex,
                        "Network idle was not reached within the settle window for {Url}. Continuing with captured DOM.",
                        normalizedUrl);
                }
            }
            catch (PlaywrightException ex) when (IsTimeoutException(ex))
            {
                throw new TimeoutException("Web scraping timed out.", ex);
            }

            if (response is null)
            {
                throw new InvalidOperationException("Web scraping did not receive a response.");
            }

            if (!response.Ok)
            {
                throw new InvalidOperationException($"Web scraping failed with status {response.Status}.");
            }

            var content = await page.ContentAsync().ConfigureAwait(false);
            var title = await page.TitleAsync().ConfigureAwait(false);
            var sanitized = WebScrapingUtilities.SanitizeHtml(content);
            var mimeType = response.Headers.TryGetValue("content-type", out var value)
                ? value
                : "text/html";

            return new WebScrapeResult(
                normalizedUrl,
                title,
                sanitized,
                mimeType,
                DateTimeOffset.UtcNow);
        }
        finally
        {
            await page.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }
    }

    private static bool IsTimeoutException(PlaywrightException exception)
    {
        var message = exception.Message ?? string.Empty;
        return message.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ERR_NETWORK_IO_SUSPENDED", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ERR_TIMED_OUT", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record WebScrapeResult(
    string Url,
    string Title,
    string HtmlContent,
    string MimeType,
    DateTimeOffset CapturedAt);

public sealed class WebScrapingOptions
{
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryCount { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 250;
    public int CircuitBreakerFailures { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}

public static class WebScrapingUtilities
{
    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL must be provided.", nameof(url));
        }

        var trimmed = url.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("URL must be a valid absolute URI.", nameof(url));
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("URL must use http or https scheme.", nameof(url));
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        return builder.Uri.ToString();
    }

    public static string SanitizeHtml(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException("HTML content must be provided.", nameof(htmlContent));
        }

        var sanitized = htmlContent;

        // Remove script tags.
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            "<script[^>]*>[\\s\\S]*?</script>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove on* event handler attributes.
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            "\\son\\w+\\s*=\\s*\"[^\"]*\"",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            "\\son\\w+\\s*=\\s*'[^']*'",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return sanitized;
    }
}
