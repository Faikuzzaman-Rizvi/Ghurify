using System.ComponentModel.DataAnnotations;

namespace Ghurify.Api.Configuration;

/// <summary>
/// Origins the React app is served from. Strict by design: no wildcard origin is ever
/// allowed, because the API answers credentialed requests.
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Policy name used when the pipeline applies CORS.</summary>
    public const string PolicyName = "GhurifyWeb";

    [Required, MinLength(1, ErrorMessage = "Cors:AllowedOrigins must list at least one origin.")]
    public string[] AllowedOrigins { get; set; } = [];
}
