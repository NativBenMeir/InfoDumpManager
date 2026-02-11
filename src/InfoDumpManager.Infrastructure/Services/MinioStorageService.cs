using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Services.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace InfoDumpManager.Infrastructure.Services;

public sealed class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IOptions<MinioOptions> options, ILogger<MinioStorageService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException("MinIO endpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("MinIO credentials are not configured.");
        }

        _client = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithSSL(_options.UseSsl)
            .Build();
    }

    public async Task<string> UploadSnapshotAsync(string objectKey, string htmlContent, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ArgumentException("Object key is required.", nameof(objectKey));
        }

        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException("HTML content is required.", nameof(htmlContent));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        await EnsureBucketAsync(cancellationToken).ConfigureAwait(false);

        var bytes = Encoding.UTF8.GetBytes(htmlContent);
        await using var stream = new MemoryStream(bytes);

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);

        await _client.PutObjectAsync(putObjectArgs, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Stored snapshot {ObjectKey} in MinIO bucket {Bucket}.", objectKey, _options.BucketName);
        return objectKey;
    }

    public async Task<string> GetSnapshotAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ArgumentException("Object key is required.", nameof(objectKey));
        }

        await EnsureBucketAsync(cancellationToken).ConfigureAwait(false);

        await using var memoryStream = new MemoryStream();
        var getArgs = new GetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectKey)
            .WithCallbackStream(async stream =>
            {
                await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            });

        await _client.GetObjectAsync(getArgs, cancellationToken).ConfigureAwait(false);

        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8, true, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException("MinIO bucket name is not configured.");
        }

        var existsArgs = new BucketExistsArgs().WithBucket(_options.BucketName);
        var exists = await _client.BucketExistsAsync(existsArgs, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            var makeArgs = new MakeBucketArgs().WithBucket(_options.BucketName);
            await _client.MakeBucketAsync(makeArgs, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Created MinIO bucket {Bucket}.", _options.BucketName);
        }
    }
}

public sealed class MinioOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "gem-snapshots";
    public bool UseSsl { get; set; }
}
