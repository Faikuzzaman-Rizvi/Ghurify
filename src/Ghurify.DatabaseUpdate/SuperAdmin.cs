using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Ghurify.DatabaseUpdate;

/// <summary>
/// Makes the first super admin.
///
/// A new deployment has nobody on the admin desk, and the portal is the only way to put somebody
/// there — so the first one has to come from outside it. This is that door: a deliberate console
/// command, run by whoever holds the database credentials, which is the same level of trust as
/// being a super admin in the first place.
///
/// It is not a migration. It runs only when asked for by name
/// (<c>dotnet run --project src/Ghurify.DatabaseUpdate -- superadmin someone@example.com</c>),
/// changes one row, and says plainly what it did. After that, super admins grant the role to
/// each other from the portal, where every change is audited.
///
/// The account has to exist and be confirmed already: this grants a role, it never creates a
/// login or sets a password.
/// </summary>
internal static class SuperAdmin
{
    public static int Grant(string connectionString, string email)
    {
        // Stored lower-cased and trimmed everywhere, so match it that way.
        var address = email.Trim().ToLowerInvariant();

        if (address.Length == 0 || !address.Contains('@', StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Ghurify: give the email address of an existing, confirmed account.");
            return 1;
        }

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = new SqlCommand(
            """
            SET XACT_ABORT ON;
            BEGIN TRAN;

            DECLARE @UserId BIGINT, @Status TINYINT, @RoleId BIGINT, @Result INT = 0;

            SELECT @UserId = [Id], @Status = [Status]
            FROM   [Main].[User] WITH (UPDLOCK, HOLDLOCK)
            WHERE  [Email] = @Email AND [Archived] = 0;

            SELECT @RoleId = [Id]
            FROM   [Main].[StaffRole]
            WHERE  [Key] = 'super-admin' AND [Archived] = 0;

            IF @UserId IS NULL
                SET @Result = 1;                -- no such account
            ELSE IF @Status <> 1
                SET @Result = 2;                -- not active (unconfirmed, suspended or closed)
            ELSE IF @RoleId IS NULL
                SET @Result = 3;                -- the role is missing: the dacpac has not been published
            ELSE IF EXISTS (SELECT 1 FROM [Main].[UserStaffRole]
                            WHERE [UserId] = @UserId AND [StaffRoleId] = @RoleId AND [Archived] = 0)
                SET @Result = 4;                -- already a super admin
            ELSE
            BEGIN
                -- GrantedById stays NULL: there was nobody to grant it yet, and that absence is
                -- itself worth recording.
                INSERT INTO [Main].[UserStaffRole] ([UserId], [StaffRoleId])
                VALUES (@UserId, @RoleId);
            END;

            COMMIT TRAN;

            SELECT @Result AS [Result], @UserId AS [UserId];
            """,
            connection);

        command.Parameters.Add("@Email", System.Data.SqlDbType.NVarChar, 256).Value = address;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            Console.Error.WriteLine("Ghurify: the database did not answer. Nothing was changed.");
            return 1;
        }

        var result = reader.GetInt32(0);
        var userId = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);

        switch (result)
        {
            case 0:
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ghurify: account {userId} is now a super admin. Sign in and open /admin."));
                return 0;

            case 1:
                Console.Error.WriteLine(
                    "Ghurify: there is no account with that address. Register it first, confirm the email, then run this again.");
                return 1;

            case 2:
                Console.Error.WriteLine(
                    "Ghurify: that account is not active. Confirm its email address (or reactivate it) first.");
                return 1;

            case 3:
                Console.Error.WriteLine(
                    "Ghurify: the super-admin role is missing. Publish the dacpac first (see Publish-Database.ps1).");
                return 1;

            default:
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Ghurify: account {userId} is already a super admin. Nothing to do."));
                return 0;
        }
    }
}
