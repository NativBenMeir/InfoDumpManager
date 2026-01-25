using System;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace InfoDumpManager.Infrastructure.Services;

public sealed class PlaywrightPageSnapshotService : IPageSnapshotService, IAsyncDisposable
{
    private readonly Lazy<Task<IPlaywright>> _playwrightFactory;
    private readonly ILogger<PlaywrightPageSnapshotService> _logger;

    public PlaywrightPageSnapshotService(ILogger<PlaywrightPageSnapshotService> logger)
    {
        _logger = logger;
        _playwrightFactory = new Lazy<Task<IPlaywright>>(Playwright.CreateAsync);
    }

    public async Task<PageSnapshot> CaptureAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be empty", nameof(url));
        }

        try
        {
            var playwright = await _playwrightFactory.Value.ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--disable-gpu", "--no-sandbox" }
            }).ConfigureAwait(false);

            await using var context = await browser.NewContextAsync().ConfigureAwait(false);
            var page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30_000
            }).ConfigureAwait(false);

            var content = await page.ContentAsync().ConfigureAwait(false);
            return new PageSnapshot(content, "text/html; charset=utf-8", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture snapshot for {Url}", url);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_playwrightFactory.IsValueCreated)
        {
            return;
        }

        var playwright = await _playwrightFactory.Value.ConfigureAwait(false);
        playwright.Dispose();
    }
}
