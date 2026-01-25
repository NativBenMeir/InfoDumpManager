using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

public sealed class WebUiTests : IClassFixture<WebApplicationFactory<global::InfoDumpManager.Web.Program>>
{
    private readonly HttpClient _client;

    public WebUiTests(WebApplicationFactory<global::InfoDumpManager.Web.Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    [Fact]
    public async Task GetRoot_ReturnsHelloWorld()
    {
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Hello World!");
    }
}
