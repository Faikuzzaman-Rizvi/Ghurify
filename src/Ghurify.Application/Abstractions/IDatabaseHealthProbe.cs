namespace Ghurify.Application.Abstractions;

/// <summary>
/// Confirms the API can reach the database. Used by the health endpoint.
/// </summary>
public interface IDatabaseHealthProbe
{
    /// <summary>
    /// Runs a trivial query. Returns true when the database answered, false when it did not.
    /// Never throws: a failed probe is a reportable state, not an error.
    /// </summary>
    Task<bool> CanReachDatabaseAsync(CancellationToken cancellationToken);
}
