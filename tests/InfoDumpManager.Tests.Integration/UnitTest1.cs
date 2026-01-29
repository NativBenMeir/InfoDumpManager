using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Serilog;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

public class DockerComposeServiceTests
{
    [Fact]
    public async Task PostgreSql_IsReachable()
    {
        await AssertPortOpen("localhost", 5432, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Redis_IsReachable()
    {
        await AssertPortOpen("localhost", 6379, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MinIoConsole_IsReachable()
    {
        await AssertPortOpen("localhost", 9001, TimeSpan.FromSeconds(5));
    }

    private static async Task AssertPortOpen(string host, int port, TimeSpan timeout)
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            await client.ConnectAsync(host, port, cts.Token);
        }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to connect to {host}:{port}. Ensure Docker Compose is running. Error: {ex.Message}");
            }
    }
}

public class SerilogLoggingTests
{
    [Fact]
    public void Serilog_WritesToConsoleAndFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "InfoDumpManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var logPath = Path.Combine(tempDir, "serilog-test-.log");

        var originalOut = Console.Out;
        using var consoleWriter = new StringWriter();
        Console.SetOut(consoleWriter);

        try
        {
            using var logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            const string message = "Serilog test message";
            logger.Information(message);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var consoleOutput = consoleWriter.ToString();
        Assert.Contains("Serilog test message", consoleOutput);

        var logFiles = Directory.GetFiles(tempDir, "serilog-test-*.log");
        Assert.True(logFiles.Length > 0, "Expected a log file to be created.");

        var fileContents = File.ReadAllText(logFiles[0]);
        Assert.Contains("Serilog test message", fileContents);
    }
}

public class ApiDocumentationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiDocumentationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SwaggerUi_IsAccessible()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/index.html");

        Assert.True(
            response.IsSuccessStatusCode,
            $"Swagger UI not accessible. Status: {(int)response.StatusCode} {response.ReasonPhrase}");
    }
}