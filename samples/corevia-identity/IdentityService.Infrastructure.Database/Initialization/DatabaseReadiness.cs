using IdentityService.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Database.Initialization;

/// <summary>
/// Readiness probe for the own-storage database: EF Core <c>CanConnectAsync</c> bounded by a short timeout.
/// Never throws for an unavailable database; it returns <c>false</c> so hosts can answer 503.
/// </summary>
public static class DatabaseReadiness
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public static async Task<bool> CanConnectAsync(
        this IdentityDbContext context,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout ?? DefaultTimeout);
        try
        {
            return await context.Database.CanConnectAsync(linked.Token).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout, provider failure or missing configuration (for example no database password):
            // the instance is simply not ready.
            return false;
        }
    }
}
