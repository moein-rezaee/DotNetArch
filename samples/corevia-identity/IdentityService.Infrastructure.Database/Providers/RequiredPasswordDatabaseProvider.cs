using Corevia.Kit.DatabaseConnection.Abstractions;
using Corevia.Kit.DatabaseConnection.Core;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Database.Providers;

/// <summary>
/// Decorates a Kit <see cref="IDatabaseProvider"/> so a missing database password fails fast with an
/// <see cref="InvalidOperationException"/> naming the configuration/secret key, instead of silently
/// falling back to a hardcoded default or an empty credential. The password is a secret: it must come
/// from the environment or the secret store (<c>POSTGRES_PASSWORD</c> / <c>SQLSERVER_PASSWORD</c>).
/// <paramref name="isRequired"/> decides per configuration whether a password is mandatory: Postgres always
/// requires one, while SQL Server requires one only when a username is supplied (no username and no password
/// keeps the Kit's Integrated Security behavior).
/// </summary>
internal sealed class RequiredPasswordDatabaseProvider(
    IDatabaseProvider inner,
    string secretKey,
    Func<DatabaseConnectionSettings, bool>? isRequired = null) : IDatabaseProvider
{
    public DatabaseProviderKind Kind => inner.Kind;

    public DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder optionsBuilder,
        DatabaseConnectionSettings settings,
        DatabaseProviderContext context)
    {
        if ((isRequired?.Invoke(settings) ?? true) && string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException(
                $"The {Kind} database password is not configured. Set the secret '{secretKey}' " +
                "(environment variable or secret store); no default password is provided.");
        }

        return inner.Configure(optionsBuilder, settings, context);
    }
}
