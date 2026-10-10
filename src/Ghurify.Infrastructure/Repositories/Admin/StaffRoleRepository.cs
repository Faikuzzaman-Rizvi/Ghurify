using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Admin;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Admin;

/// <summary>
/// The admin desk's roles and who holds them.
///
/// Permission keys are read through <see cref="PermissionCatalog.Known"/>, so a key the database
/// holds but this build does not define is dropped on the way in. Rolling the API back can then
/// only ever narrow what somebody may do, never widen it.
/// </summary>
public sealed class StaffRoleRepository(IDbConnectionFactory connectionFactory) : IStaffRoleRepository
{
    public async Task<IReadOnlyList<StaffRoleRecord>> QueryRolesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.QueryStaffRoles,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var roles = (await results.ReadAsync<RoleRow>()).ToList();
        var permissions = (await results.ReadAsync<RolePermissionRow>())
            .GroupBy(row => row.StaffRoleId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Permission));

        return [.. roles.Select(row => Map(
            row,
            permissions.TryGetValue(row.Id, out var keys) ? keys : []))];
    }

    public async Task<StaffRoleRecord?> FindRoleAsync(long id, CancellationToken cancellationToken) =>
        (await QueryRolesAsync(cancellationToken)).FirstOrDefault(role => role.Id == id);

    public async Task<StaffRoleRecord?> FindRoleByKeyAsync(string key, CancellationToken cancellationToken) =>
        (await QueryRolesAsync(cancellationToken))
            .FirstOrDefault(role => string.Equals(role.Key, key, StringComparison.Ordinal));

    public async Task<SaveStaffRoleOutcome> SaveRoleAsync(
        long? id,
        StaffRoleEdit edit,
        long actorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        using var permissions = new DataTable();
        permissions.Columns.Add("Permission", typeof(string));
        foreach (var permission in edit.Permissions)
        {
            permissions.Rows.Add(permission);
        }

        var parameters = new DynamicParameters(new
        {
            Id = id,
            Key = new DbString { Value = edit.Key, IsAnsi = true, Length = 40 },
            edit.Name,
            edit.NameBn,
            edit.Description,
            edit.DescriptionBn,
            ActorId = actorId,
        });

        parameters.Add("Permissions", permissions.AsTableValuedParameter("[Main].[PermissionList]"));
        parameters.Add("Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        // The procedure selects the role id on success; a refusal selects nothing, so the read
        // has to tolerate an empty result.
        _ = await connection.QueryFirstOrDefaultAsync<long?>(new CommandDefinition(
            Procedures.Main.SetStaffRole,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return (SaveStaffRoleOutcome)parameters.Get<byte>("Result");
    }

    public async Task<DeleteStaffRoleOutcome> DeleteRoleAsync(
        long id,
        long actorId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters(new { Id = id, ActorId = actorId });
        parameters.Add("MemberCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.DelStaffRole,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return (DeleteStaffRoleOutcome)parameters.Get<byte>("Result");
    }

    public async Task<IReadOnlyList<StaffMember>> QueryMembersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.QueryStaffMembers,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var members = (await results.ReadAsync<MemberRow>()).ToList();
        var held = (await results.ReadAsync<HeldRoleRow>())
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Select(Map).ToList());

        return [.. members.Select(row => new StaffMember(
            row.Id,
            row.Email,
            row.DisplayName,
            (UserStatus)row.Status,
            Utc(row.AvatarUpdatedOn),
            Utc(row.StaffSince)!.Value,
            held.TryGetValue(row.Id, out var roles) ? roles : []))];
    }

    public async Task<IReadOnlyList<StaffRoleHeld>> QueryHeldRolesAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<HeldRoleRow>(new CommandDefinition(
            """
            SELECT    [s].[Id] AS [StaffRoleId], [s].[Key], [s].[Name], [s].[NameBn],
                      [s].[IsSuperAdmin], [h].[Created] AS [GrantedOn], [h].[GrantedById],
                      [g].[DisplayName] AS [GrantedByName], [h].[UserId]
            FROM      [Main].[UserStaffRole] AS [h]
            JOIN      [Main].[StaffRole]     AS [s] ON [s].[Id] = [h].[StaffRoleId] AND [s].[Archived] = 0
            LEFT JOIN [Main].[User]          AS [g] ON [g].[Id] = [h].[GrantedById]
            WHERE     [h].[UserId] = @UserId
              AND     [h].[Archived] = 0
            ORDER BY  [s].[IsSuperAdmin] DESC, [s].[Name] ASC;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(Map)];
    }

    public async Task<AssignStaffRoleOutcome> SetMemberRoleAsync(
        long userId,
        long staffRoleId,
        bool grant,
        long actorId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters(new
        {
            UserId = userId,
            StaffRoleId = staffRoleId,
            Grant = grant,
            ActorId = actorId,
        });

        parameters.Add("Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetUserStaffRole,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return (AssignStaffRoleOutcome)parameters.Get<byte>("Result");
    }

    private static StaffRoleRecord Map(RoleRow row, IEnumerable<string> permissions) =>
        new(row.Id,
            row.Key,
            row.Name,
            row.NameBn,
            row.Description,
            row.DescriptionBn,
            row.IsSystem,
            row.IsSuperAdmin,
            row.MemberCount,
            PermissionCatalog.Known(permissions),
            Utc(row.Created)!.Value);

    private static StaffRoleHeld Map(HeldRoleRow row) =>
        new(row.StaffRoleId,
            row.Key,
            row.Name,
            row.NameBn,
            row.IsSuperAdmin,
            Utc(row.GrantedOn)!.Value,
            row.GrantedById,
            row.GrantedByName);

    private static DateTimeOffset? Utc(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    /// <summary>
    /// The columns of <c>Main.QueryStaffRoles</c>' first result set, in the order it selects them.
    /// Dapper matches a record's constructor to the result set positionally, so a parameter out of
    /// order is not mapped by name: it fails the whole read with "a parameterless default
    /// constructor or one matching signature ... is required". Keep this list and the procedure's
    /// SELECT in the same order. <c>MemberCount</c> is computed, so it comes last in both.
    /// </summary>
    private sealed record RoleRow(
        long Id,
        string Key,
        string Name,
        string NameBn,
        string? Description,
        string? DescriptionBn,
        bool IsSystem,
        bool IsSuperAdmin,
        DateTime Created,
        int MemberCount);

    private sealed record RolePermissionRow(long StaffRoleId, string Permission);

    private sealed record MemberRow(
        long Id,
        string Email,
        string? DisplayName,
        byte Status,
        DateTime? AvatarUpdatedOn,
        DateTime StaffSince);

    private sealed record HeldRoleRow(
        long UserId,
        long StaffRoleId,
        string Key,
        string Name,
        string NameBn,
        bool IsSuperAdmin,
        DateTime GrantedOn,
        long? GrantedById,
        string? GrantedByName);
}
