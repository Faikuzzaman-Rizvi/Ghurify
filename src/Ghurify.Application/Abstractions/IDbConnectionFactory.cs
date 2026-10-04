using System.Data.Common;

namespace Ghurify.Application.Abstractions;

/// <summary>
/// Hands out a new, already-open database connection. Repositories open one per method and
/// dispose it with <c>await using</c>; connections are never cached or shared between calls.
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}
