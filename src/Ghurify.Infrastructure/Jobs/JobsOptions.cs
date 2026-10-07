namespace Ghurify.Infrastructure.Jobs;

/// <summary>
/// Background work. With <see cref="Enabled"/> off (integration tests, the OpenAPI generator) no
/// scheduler is configured, recurring jobs never run, and queued jobs run inline. With
/// <see cref="RunServer"/> off the process can queue jobs but not process them, so the web API and
/// the jobs worker can be scaled separately from the same build.
/// </summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    public bool Enabled { get; set; } = true;

    public bool RunServer { get; set; } = true;

    /// <summary>Worker threads per server. Jobs are short database calls; a few are plenty.</summary>
    public int WorkerCount { get; set; } = 4;
}
