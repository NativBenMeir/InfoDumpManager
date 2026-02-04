using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InfoDumpManager.Application.Agents.Orchestration;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Tests.Integration.Fixtures;
using InfoDumpManager.WebAPI;
using InfoDumpManager.WebAPI.Contracts.Ai;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class AiProcessingApiIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestcontainerFixture _fixture;
    private readonly InfoDumpWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _gemId = Guid.NewGuid();

    public AiProcessingApiIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
        Environment.SetEnvironmentVariable("JWT_SECRET", "TestJwtSecretKey1234567890123456");
        _factory = new InfoDumpWebApplicationFactory(_fixture, TestTenantId, _gemId);
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ProcessEndpoint_ShouldReturnAcceptedWithJobId()
    {
        // Arrange
        var request = new AiProcessRequest
        {
            GemId = _gemId,
            ContentText = "AI processing content",
            RunValidation = false
        };

        // Act
        var response = await PostJsonAsync("/api/ai/process", request);

        // Assert
        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Expected Accepted but got {response.StatusCode}. Body: {error}");
        }

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.True(payload!.RootElement.TryGetProperty("jobId", out var jobIdElement));
        var jobId = jobIdElement.GetGuid();
        Assert.NotEqual(Guid.Empty, jobId);

        var statusResponse = await GetAsync($"/api/ai/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusPayload = await statusResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(statusPayload);
        Assert.True(statusPayload!.RootElement.TryGetProperty("jobId", out _));
    }

    private Task<HttpResponseMessage> PostJsonAsync<T>(string uri, T request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request)
        };

        return _client.SendAsync(message);
    }

    private Task<HttpResponseMessage> GetAsync(string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return _client.SendAsync(request);
    }

    private sealed class InfoDumpWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly PostgresTestcontainerFixture _fixture;
        private readonly Guid _tenantId;
        private readonly Guid _gemId;

        public InfoDumpWebApplicationFactory(PostgresTestcontainerFixture fixture, Guid tenantId, Guid gemId)
        {
            _fixture = fixture;
            _tenantId = tenantId;
            _gemId = gemId;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                    ["JwtSettings:Issuer"] = "InfoDumpManager",
                    ["JwtSettings:Audience"] = "InfoDumpManagerAPI",
                    ["JwtSettings:Secret"] = "TestJwtSecretKey1234567890123456",
                    ["JwtSettings:ExpiresMinutes"] = "60"
                };

                config.AddInMemoryCollection(overrides!);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUnitOfWork>();

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultScheme = "Test";
                });

                var gem = GEM.Create(
                    _tenantId,
                    "AI Processing Gem",
                    $"https://example.com/gem/{_gemId:N}",
                    new GEMSource("https://example.com/source", "Integration"),
                    new GEMSnapshot("Initial content", "text/plain", DateTimeOffset.UtcNow));

                var gemRepo = new Mock<IGEMRepository>();
                gemRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(gem);

                var categoryRepo = new Mock<ICategoryRepository>();
                var activityRepo = new Mock<IActivityLogRepository>();

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(x => x.GEMs).Returns(gemRepo.Object);
                unitOfWork.SetupGet(x => x.Categories).Returns(categoryRepo.Object);
                unitOfWork.SetupGet(x => x.ActivityLogs).Returns(activityRepo.Object);

                services.AddSingleton(unitOfWork.Object);
            });
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("tenant_id", TestTenantId.ToString())
            }, "Test");

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
