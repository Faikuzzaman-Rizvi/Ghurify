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
}
