namespace InfoDumpManager.Infrastructure.Configuration;

public sealed class MinioOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = "snapshots";
    public bool UseSsl { get; init; }
    public string Region { get; init; } = string.Empty;
}
