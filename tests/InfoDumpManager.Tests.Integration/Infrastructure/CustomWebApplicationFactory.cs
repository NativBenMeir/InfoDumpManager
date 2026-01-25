using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using InfoDumpManager.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfoDumpManager.Tests.Integration.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<global::Program>
{
    private readonly PostgreSqlTestcontainer _postgres;
    private readonly string _jwtSecret;

    public CustomWebApplicationFactory(PostgreSqlTestcontainer postgres, string jwtSecret = "IntegrationSecretKey0123456789")
    {
        _postgres = postgres;
        _jwtSecret = jwtSecret;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.ConnectionString,
                ["Jwt:Secret"] = _jwtSecret,
                ["Jwt:Issuer"] = "InfoDumpManager-Test",
                ["Jwt:Audience"] = "InfoDumpManager-Test",
                ["Minio:Endpoint"] = "http://localhost",
                ["Minio:AccessKey"] = "minioadmin",
                ["Minio:SecretKey"] = "minioadmin",
                ["Minio:BucketName"] = "test-bucket",
                ["Minio:UseSsl"] = "false"
            };

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPageSnapshotService>();
            services.RemoveAll<ISnapshotStorageService>();
            services.AddSingleton<IPageSnapshotService, DummyPageSnapshotService>();
            services.AddSingleton<ISnapshotStorageService, DummySnapshotStorageService>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.TestScheme, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
            });

            services.AddSingleton<IPolicyEvaluator, TestPolicyEvaluator>();
        });
    }

    private sealed class DummyPageSnapshotService : IPageSnapshotService
    {
        public Task<PageSnapshot> CaptureAsync(string url, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PageSnapshot("<html></html>", "text/html", DateTime.UtcNow));
        }
    }

    private sealed class DummySnapshotStorageService : ISnapshotStorageService
    {
        public Task<Uri> StoreSnapshotAsync(string objectName, Stream data, string contentType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Uri($"http://localhost/snapshots/{objectName}"));
        }
    }

    #pragma warning disable CS0618
    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string TestScheme = "IntegrationTestScheme";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            };

            var identity = new ClaimsIdentity(claims, TestScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, TestScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
    #pragma warning restore CS0618

    private sealed class TestPolicyEvaluator : IPolicyEvaluator
    {
        public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TestAuthHandler.TestScheme));
            var ticket = new AuthenticationTicket(principal, TestAuthHandler.TestScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy, AuthenticateResult authenticationResult, HttpContext context, object? resource)
        {
            return Task.FromResult(PolicyAuthorizationResult.Success());
        }
    }
}
