using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Site;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Site;

/// <summary>
/// The site's own settings and images.
///
/// Reads only the rows that differ from the shipped defaults, so the common case — a site that
/// has changed a name and a colour — is a handful of rows however many settings exist.
/// </summary>
public sealed class SiteSettingsRepository(IDbConnectionFactory connectionFactory) : ISiteSettingsRepository
{
    public async Task<StoredSiteSettings> QueryAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Site.QuerySettings,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var values = (await results.ReadAsync<SettingRow>())
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);

        var assets = (await results.ReadAsync<AssetStampRow>())
            .Select(row => new SiteAssetStamp(row.Kind, row.ContentType, row.SizeBytes, Utc(row.UpdatedOn)))
            .ToList();

        return new StoredSiteSettings(values, assets);
    }

    public async Task<IReadOnlyList<SettingChange>> SaveAsync(
        IReadOnlyDictionary<string, string> settings,
        long actorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        using var table = new DataTable();
        table.Columns.Add("Key", typeof(string));
        table.Columns.Add("Value", typeof(string));
        foreach (var (key, value) in settings)
        {
            table.Rows.Add(key, value);
        }

        var parameters = new DynamicParameters(new { ActorId = actorId });
        parameters.Add("Settings", table.AsTableValuedParameter("[Site].[SettingList]"));

        var changes = await connection.QueryAsync<SettingChange>(new CommandDefinition(
            Procedures.Site.SetSettings,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. changes];
    }

    public async Task<IReadOnlyList<SettingChangeRecord>> QueryHistoryAsync(
        string key,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<HistoryRow>(new CommandDefinition(
            Procedures.Site.QuerySettingHistory,
            new { Key = new DbString { Value = key, IsAnsi = true, Length = 60 }, Take = take },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new SettingChangeRecord(
            row.Id,
            row.Key,
            row.OldValue,
            row.NewValue,
            row.ChangedById,
            row.ChangedByName,
            Utc(row.Created)))];
    }

    public async Task<SiteAsset?> GetAssetAsync(string kind, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<AssetRow>(new CommandDefinition(
            Procedures.Site.GetAsset,
            new { Kind = new DbString { Value = kind, IsAnsi = true, Length = 30 } },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new SiteAsset(row.Kind, row.ContentType, row.Bytes, Utc(row.UpdatedOn));
    }

    public async Task SaveAssetAsync(
        string kind,
        string contentType,
        byte[] bytes,
        long actorId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters(new
        {
            Kind = new DbString { Value = kind, IsAnsi = true, Length = 30 },
            ContentType = new DbString { Value = contentType, IsAnsi = true, Length = 40 },
            Bytes = bytes,
            ActorId = actorId,
        });

        parameters.Add("Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Site.SetAsset,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> RemoveAssetAsync(string kind, long actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters(new
        {
            Kind = new DbString { Value = kind, IsAnsi = true, Length = 30 },
            ActorId = actorId,
        });

        parameters.Add("Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Site.DelAsset,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<byte>("Result") == 0;
    }

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record SettingRow(string Key, string Value);

    private sealed record AssetStampRow(string Kind, string ContentType, int SizeBytes, DateTime UpdatedOn);

    private sealed record AssetRow(string Kind, string ContentType, byte[] Bytes, int SizeBytes, DateTime UpdatedOn);

    private sealed record HistoryRow(
        long Id,
        string Key,
        string? OldValue,
        string? NewValue,
        long ChangedById,
        string? ChangedByName,
        DateTime Created);
}
