using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Tests.Integration.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class MinioStorageIntegrationTests
{
    private readonly MinioTestcontainerFixture _fixture;

    public MinioStorageIntegrationTests(MinioTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MinioStorage_UploadSnapshot_ReturnsKey()
    {
        var service = CreateService();
        var key = "snapshots/test-upload.html";

        var result = await service.UploadSnapshotAsync(key, "<html>Upload</html>", "text/html");

        result.Should().Be(key);
    }

    [Fact]
    public async Task MinioStorage_RetrieveSnapshot_ReturnsOriginalHtml()
    {
        var service = CreateService();
        var key = "snapshots/test-retrieve.html";
        const string html = "<html><body>Snapshot</body></html>";

        await service.UploadSnapshotAsync(key, html, "text/html");
        var loaded = await service.GetSnapshotAsync(key);

        loaded.Should().Be(html);
    }

    private MinioStorageService CreateService()
    {
        var options = Options.Create(new MinioOptions
        {
            Endpoint = _fixture.Endpoint,
            AccessKey = _fixture.UserName,
            SecretKey = _fixture.Password,
            BucketName = _fixture.BucketName,
            UseSsl = false
        });

        return new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);
    }
}
