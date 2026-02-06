using System.Threading.RateLimiting;
using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimiting;

namespace InfoDumpManager.Infrastructure.Services.LLM;

public sealed class TenantRateLimiter : ILLMRateLimiter, IDisposable
{
    private static readonly ResiliencePropertyKey<string> TenantKey = new("tenant-id");

    private readonly PartitionedRateLimiter<ResilienceContext> _limiter;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<TenantRateLimiter> _logger;
    private bool _disposed;

    public TenantRateLimiter(IOptions<LLMRateLimitOptions> options, ILogger<TenantRateLimiter> logger)
    {
        _logger = logger;
        var resolved = options.Value;

        _limiter = PartitionedRateLimiter.Create<ResilienceContext, string>(context =>
        {
            var partitionKey = context.Properties.TryGetValue(TenantKey, out var tenant)
                ? tenant
                : "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = resolved.PermitLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = resolved.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
        });

        _pipeline = new ResiliencePipelineBuilder()
            .AddRateLimiter(new RateLimiterStrategyOptions
            {
                RateLimiter = args => _limiter.AcquireAsync(args.Context, 1, args.Context.CancellationToken),
                OnRejected = args =>
                {
                    if (args.Context.Properties.TryGetValue(TenantKey, out var tenant))
                    {
                        _logger.LogWarning("LLM rate limit exceeded for tenant {TenantId}", tenant);
                    }
                    else
                    {
                        _logger.LogWarning("LLM rate limit exceeded for unknown tenant");
                    }

                    return default;
                }
            })
            .Build();
    }

    public Task<T> ExecuteAsync<T>(
        Guid tenantId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(TenantKey, tenantId.ToString());

        return ExecuteInternalAsync(action, context);
    }

    private async Task<T> ExecuteInternalAsync<T>(Func<CancellationToken, Task<T>> action, ResilienceContext context)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async ctx => await action(ctx.CancellationToken).ConfigureAwait(false),
                context)
                .ConfigureAwait(false);
        }
        catch (RateLimiterRejectedException)
        {
            throw;
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _limiter.Dispose();
        _disposed = true;
    }
}
