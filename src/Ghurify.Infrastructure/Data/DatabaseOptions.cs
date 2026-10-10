using System.ComponentModel.DataAnnotations;

namespace Ghurify.Infrastructure.Data;

/// <summary>
/// Database settings. Required values are validated at startup so a misconfigured
/// deployment fails immediately instead of at the first request.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Connection string for the Ghurify database. Supplied by user-secrets locally and by
    /// App Service settings / Key Vault when deployed. Never checked into the repository.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Database:ConnectionString is required.")]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Seconds a command may run before it is cancelled.</summary>
    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Connections kept open even while idle. The pool is otherwise empty after a quiet spell, and
    /// every request that finds it empty pays a full TCP connect plus SQL login before its query
    /// runs: measured at 60-130 ms against the shared server, against 10 ms on a pooled
    /// connection. Holding a few open makes that cost disappear from the request path.
    /// </summary>
    [Range(0, 100)]
    public int MinPoolSize { get; set; } = 8;

    /// <summary>
    /// Ceiling on pooled connections. Reached, further requests queue for
    /// <see cref="ConnectTimeoutSeconds"/> rather than opening unbounded connections and
    /// exhausting the server's worker threads.
    /// </summary>
    [Range(10, 1000)]
    public int MaxPoolSize { get; set; } = 200;

    /// <summary>
    /// Seconds to wait for a connection, SqlClient's own default.
    ///
    /// Deliberately not lowered. A shorter budget looks attractive — a down database would
    /// surface as an error instead of a spinner — but it was tried at 5 seconds and turned a
    /// momentary stall on the shared server into a failed startup: the TLS pre-login handshake
    /// occasionally takes longer than that, and Hangfire's schema check at startup is fatal when
    /// it cannot connect. The long hangs this was meant to cure came from
    /// <see cref="ConnectRetrySeconds"/>, which is where they are actually fixed.
    ///
    /// In normal running this hardly matters: connections come from a pool that is already warm,
    /// so this budget only applies when the pool is empty or the server is genuinely unreachable.
    /// </summary>
    [Range(1, 120)]
    public int ConnectTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Extra attempts SqlClient makes when a connection is refused, and the pause between them.
    /// The pause matters more than the count: a connection string asking for 3 retries 10 seconds
    /// apart turns one blip into a 35-second request, which reads to the user as a hung site.
    /// </summary>
    [Range(0, 10)]
    public int ConnectRetryCount { get; set; } = 2;

    /// <summary>Seconds between connection retries. See <see cref="ConnectRetryCount"/>.</summary>
    [Range(1, 60)]
    public int ConnectRetrySeconds { get; set; } = 1;

    /// <summary>
    /// Opens <see cref="MinPoolSize"/> connections while the host starts, so the first visitor
    /// does not pay for filling the pool. Off in tests, which must not touch a database to start.
    /// </summary>
    public bool WarmUpPoolOnStart { get; set; } = true;
}
