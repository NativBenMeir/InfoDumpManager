using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Tests.Integration.Fixtures;
using InfoDumpManager.Web;
using InfoDumpManager.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using Npgsql;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class WebUiIntegrationTests
{
    private readonly PostgresTestcontainerFixture _fixture;

    public WebUiIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TEST_039_SubmitGemForm_RedirectsToDetail()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var scrapeResult = BuildScrapeResult("https://example.com/article");

        await using var factory = CreateFactory(tenantId, userId, scrapeResult);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SourceUrl"] = scrapeResult.Url
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith("/GEMs/Detail");

        await using var context = _fixture.CreateContext();
        var created = context.Gems.SingleOrDefault(x => x.TenantId == tenantId && x.Url == scrapeResult.Url);
        created.Should().NotBeNull();
    }

    [Fact]
    public async Task TEST_040_ListGems_PaginatesResults()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedGemsAsync(tenantId, 3);

        await using var factory = CreateFactory(tenantId, userId, BuildScrapeResult("https://example.com/seed"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/GEMs/List?page=1&pageSize=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        CountGemRows(html).Should().Be(2);
    }

    [Fact]
    public async Task TEST_041_FilterGemsByCategory_ReturnsMatching()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var category = Category.Create(tenantId, "Filter Category", userId);
        var gemInCategory = CreateGem(tenantId, "Gem In Category");
        gemInCategory.AssignCategory(category);
        var gemOther = CreateGem(tenantId, "Gem Other");

        await SeedAsync(category, gemInCategory, gemOther);

        await using var factory = CreateFactory(tenantId, userId, BuildScrapeResult("https://example.com/seed"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/GEMs/List?CategoryId={category.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        CountGemRows(html).Should().Be(1);
        html.Should().Contain(gemInCategory.Title);
    }

    [Fact]
    public async Task TEST_042_ViewGemDetail_ShowsFields()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gem = CreateGem(tenantId, "Detail Gem");
        await SeedAsync(gem);

        await using var factory = CreateFactory(tenantId, userId, BuildScrapeResult("https://example.com/seed"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/GEMs/Detail/{gem.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(gem.Title);
        html.Should().Contain("Assign Category");
        html.Should().Contain(gem.Url);
    }

    [Fact]
    public async Task TEST_043_AssignCategory_UpdatesGem()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gem = CreateGem(tenantId, "Assign Gem");
        var category = Category.Create(tenantId, "Assigned", userId);
        await SeedAsync(category, gem);

        await using var factory = CreateFactory(tenantId, userId, BuildScrapeResult("https://example.com/seed"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync($"/GEMs/Detail/{gem.Id}?handler=AssignCategory", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SelectedCategoryId"] = category.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        await using var context = _fixture.CreateContext();
        var updated = await context.Gems.FindAsync(gem.Id);
        updated.Should().NotBeNull();
        updated!.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public async Task TEST_044_CreateCategory_ShowsInList()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var factory = CreateFactory(tenantId, userId, BuildScrapeResult("https://example.com/seed"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/Categories/Manage?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Create.Name"] = "New Category",
            ["Create.Description"] = "Created via UI test"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        await using var context = _fixture.CreateContext();
        context.Categories.Should().Contain(c => c.TenantId == tenantId && c.Name == "New Category");
    }

    [Fact]
    public async Task TEST_045_DeleteCategory_RemovesCategory()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var category = Category.Create(tenantId, "Delete Category", userId);
        await SeedAsync(category);

        await using var factory = CreateFactory(tenantId, userId, BuildScrapeResult("https://example.com/seed"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/Categories/Manage?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["DeleteCategoryId"] = category.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        await using var context = _fixture.CreateContext();
        context.Categories.Should().NotContain(c => c.Id == category.Id);
    }

    [Fact]
    public async Task TEST_046_Accessibility_AxeHasNoViolations()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedGemsAsync(tenantId, 1);

        await using var factory = CreateFactory(tenantId, userId, BuildScrapeResult("https://example.com/seed"));
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/GEMs/List");
        var document = InjectStyles(html);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(document);
        await page.AddScriptTagAsync(new PageAddScriptTagOptions
        {
            Url = "https://cdnjs.cloudflare.com/ajax/libs/axe-core/4.9.1/axe.min.js"
        });

        var result = await page.EvaluateAsync<AxeRunResult>("async () => await axe.run()");
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task TEST_047_MobileResponsive_LayoutFitsViewport()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedGemsAsync(tenantId, 1);

        await using var factory = CreateFactory(tenantId, userId, BuildScrapeResult("https://example.com/seed"));
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/GEMs/List");
        var document = InjectStyles(html);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 375, Height = 812 }
        });

        await page.SetContentAsync(document);

        var scrollWidth = await page.EvaluateAsync<int>("() => document.body.scrollWidth");
        scrollWidth.Should().BeLessThanOrEqualTo(400);
    }

    private WebUiApplicationFactory CreateFactory(Guid tenantId, Guid userId, WebScrapeResult scrapeResult)
    {
        return new WebUiApplicationFactory(_fixture, tenantId, userId, scrapeResult);
    }

    private static int CountGemRows(string html)
    {
        return Regex.Matches(html, "data-testid=\"gem-row\"").Count;
    }

    private async Task SeedGemsAsync(Guid tenantId, int count)
    {
        var gems = Enumerable.Range(1, count)
            .Select(i => CreateGem(tenantId, $"Gem {i}") )
            .ToArray();

        await SeedAsync(gems.Cast<object>().ToArray());
    }

    private async Task SeedAsync(params object[] entities)
    {
        await using var context = _fixture.CreateContext();

        foreach (var entity in entities)
        {
            context.Add(entity);
        }

        await context.SaveChangesAsync();
    }

    private static GEM CreateGem(Guid tenantId, string title)
    {
        var source = new GEMSource("https://example.com/source", "Source");
        var snapshot = new GEMSnapshot("<html><body>Snapshot</body></html>", "text/html", DateTimeOffset.UtcNow);
        return GEM.Create(tenantId, title, $"https://example.com/{Guid.NewGuid():N}", source, snapshot);
    }

    private static WebScrapeResult BuildScrapeResult(string url)
    {
        return new WebScrapeResult(url, "Test Title", "<html><body>Test</body></html>", "text/html", DateTimeOffset.UtcNow);
    }

    private static string InjectStyles(string html)
    {
        var bootstrapCss = ReadWebAsset("wwwroot/lib/bootstrap/dist/css/bootstrap.min.css");
        var siteCss = ReadWebAsset("wwwroot/css/site.css");
        var styleTag = $"<style>{bootstrapCss}\n{siteCss}</style>";

        return html.Replace("</head>", styleTag + "</head>", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadWebAsset(string relativePath)
    {
        var root = LocateRepositoryRoot();
        var fullPath = Path.Combine(root, "src", "InfoDumpManager.Web", relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
    }

    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "InfoDumpManager.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Could not locate repository root.");
        }

        return directory.FullName;
    }

    private sealed class WebUiApplicationFactory : WebApplicationFactory<WebAppEntryPoint>
    {
        private readonly PostgresTestcontainerFixture _fixture;
        private readonly Guid _tenantId;
        private readonly Guid _userId;
        private readonly WebScrapeResult _scrapeResult;

        public WebUiApplicationFactory(PostgresTestcontainerFixture fixture, Guid tenantId, Guid userId, WebScrapeResult scrapeResult)
        {
            _fixture = fixture;
            _tenantId = tenantId;
            _userId = userId;
            _scrapeResult = scrapeResult;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                    ["WebUserContext:TenantId"] = _tenantId.ToString(),
                    ["WebUserContext:UserId"] = _userId.ToString(),
                    ["WebScraping:TimeoutSeconds"] = "1"
                };

                config.AddInMemoryCollection(settings);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(_fixture.ConnectionString);
                dataSourceBuilder.UseVector();
                var dataSource = dataSourceBuilder.Build();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(dataSource, sql =>
                    {
                        sql.EnableRetryOnFailure();
                        sql.UseVector();
                    }));
                services.Configure<WebUserContextOptions>(options =>
                {
                    options.TenantId = _tenantId;
                    options.UserId = _userId;
                });
                services.RemoveAll<IWebScrapingService>();
                services.AddSingleton<IWebScrapingService>(new TestWebScrapingService(_scrapeResult));
                services.Configure<RazorPagesOptions>(options =>
                {
                    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
                });
            });
        }
    }

    private sealed class TestWebScrapingService : IWebScrapingService
    {
        private readonly WebScrapeResult _result;

        public TestWebScrapingService(WebScrapeResult result)
        {
            _result = result;
        }

        public Task<WebScrapeResult> ScrapeAsync(string url, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result with { Url = url });
        }
    }

    private sealed class AxeRunResult
    {
        public AxeViolation[] Violations { get; set; } = Array.Empty<AxeViolation>();
    }

    private sealed class AxeViolation
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Help { get; set; } = string.Empty;
        public string HelpUrl { get; set; } = string.Empty;
        public AxeNode[] Nodes { get; set; } = Array.Empty<AxeNode>();
    }

    private sealed class AxeNode
    {
        public string Html { get; set; } = string.Empty;
        public string[] Target { get; set; } = Array.Empty<string>();
    }
}
