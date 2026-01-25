using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Services;
using InfoDumpManager.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;

namespace InfoDumpManager.Infrastructure.Services;

public sealed class MinioSnapshotStorageService : ISnapshotStorageService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioSnapshotStorageService> _logger;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketInitialized;

    public MinioSnapshotStorageService(IOptions<MinioOptions> options, ILogger<MinioSnapshotStorageService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        ValidateOptions();

        var clientBuilder = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (_options.UseSsl)
        {
            clientBuilder = clientBuilder.WithSSL();
        }

        if (!string.IsNullOrWhiteSpace(_options.Region))
        {
            clientBuilder = clientBuilder.WithRegion(_options.Region);
        }

        _client = clientBuilder.Build();
    }

    public async Task<Uri> StoreSnapshotAsync(string objectName, Stream data, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Object name cannot be empty", nameof(objectName));
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        await EnsureBucketExistsAsync(cancellationToken).ConfigureAwait(false);

        Stream uploadStream = data;
        MemoryStream? bufferedStream = null;

        if (!data.CanSeek)
        {
            bufferedStream = new MemoryStream();
            await data.CopyToAsync(bufferedStream, cancellationToken).ConfigureAwait(false);
            bufferedStream.Position = 0;
            uploadStream = bufferedStream;
        }
        else
        {
            data.Position = 0;
        }

        try
        {
            var putArgs = new PutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName)
                .WithStreamData(uploadStream)
                .WithObjectSize(uploadStream.Length)
                .WithContentType(contentType);

            await _client.PutObjectAsync(putArgs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (bufferedStream is not null)
            {
                await bufferedStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        return BuildSnapshotUri(objectName);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException("Minio endpoint must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Minio credentials must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException("Minio bucket name must be configured.");
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (_bucketInitialized)
        {
            return;
        }

        await _bucketLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_bucketInitialized)
            {
                return;
            }

            var exists = await _client.BucketExistsAsync(new BucketExistsArgs()
                .WithBucket(_options.BucketName), cancellationToken).ConfigureAwait(false);

            if (!exists)
            {
                var makeArgs = new MakeBucketArgs().WithBucket(_options.BucketName);
                if (!string.IsNullOrWhiteSpace(_options.Region))
                {
                    makeArgs = makeArgs.WithLocation(_options.Region);
                }

                await _client.MakeBucketAsync(makeArgs, cancellationToken).ConfigureAwait(false);
            }

            _bucketInitialized = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    private Uri BuildSnapshotUri(string objectName)
    {
        var scheme = _options.UseSsl ? "https" : "http";
        if (Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpointUri))
        {
            var builder = new UriBuilder(endpointUri)
            {
                Path = $"{_options.BucketName.Trim('/')}/{objectName}"
            };

            return builder.Uri;
        }

        var trimmedEndpoint = _options.Endpoint.Trim().TrimEnd('/');
        var bucketSegment = _options.BucketName.Trim('/');
        return new Uri($"{scheme}://{trimmedEndpoint}/{bucketSegment}/{objectName}");
    }
}
