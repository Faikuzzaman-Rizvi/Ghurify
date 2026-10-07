namespace Ghurify.Application.Abstractions;

/// <summary>Whether blob storage (media and identity documents) is reachable. Never throws.</summary>
public interface IStorageHealthProbe
{
    Task<ComponentHealth> CheckAsync(CancellationToken cancellationToken);
}

/// <summary>The state of one dependency, as the readiness check reports it.</summary>
public enum ComponentHealth
{
    Healthy = 0,
    Unhealthy = 1,

    /// <summary>Optional and switched off in this environment (features that need it say so).</summary>
    NotConfigured = 2,
}
