using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace InfoDumpManager.Tests.Integration.TestUtilities;

public sealed class MockWebServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _requestCount;

    private MockWebServer(WebApplication app)
    {
        _app = app;
    }

    public string BaseUrl { get; private set; } = string.Empty;

    public int RequestCount => _requestCount;

    public static async Task<MockWebServer> StartAsync(Func<HttpContext, Task> handler)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

        var app = builder.Build();
        var server = new MockWebServer(app);

        app.Map("/{**catchall}", async context =>
        {
            Interlocked.Increment(ref server._requestCount);
            await handler(context);
        });

        await app.StartAsync();
        server.BaseUrl = app.Urls.Single();
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
