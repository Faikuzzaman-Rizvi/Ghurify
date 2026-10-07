namespace Ghurify.Infrastructure.Media;

/// <summary>
/// Blob storage for uploads. Optional: without a connection string the API starts, logs a warning,
/// and photo uploads answer "unavailable" (degrade, do not fail). Locally, Azurite from
/// docker-compose: <c>UseDevelopmentStorage=true</c>. Deployed, the account's connection string
/// from Key Vault.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string? ConnectionString { get; set; }

    public string Container { get; set; } = "media";

    /// <summary>
    /// The private container for identity documents. Separate from media on purpose: nothing that
    /// serves stories can ever list or link an ID card.
    /// </summary>
    public string DocumentsContainer { get; set; } = "identity-documents";

    /// <summary>
    /// Browser origins allowed to PUT straight to the storage account (its CORS rules). Applied on
    /// first use when set; in production the rules are managed with the storage account instead.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
