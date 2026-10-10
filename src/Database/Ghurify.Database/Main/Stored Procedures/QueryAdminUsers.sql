-- The admin people search. @Search matches the start of an email address, the end of a phone
-- number (when it is digits), or any part of the name. Newest first.
CREATE PROCEDURE [Main].[QueryAdminUsers]
    @Search    NVARCHAR (256) = NULL,
    @Status    TINYINT        = NULL,
    @Role      TINYINT        = NULL,
    @Offset    INT,
    @PageSize  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Term NVARCHAR (256) = NULLIF(LTRIM(RTRIM(@Search)), N'');
    DECLARE @Digits NVARCHAR (256) =
        CASE WHEN @Term IS NOT NULL AND @Term NOT LIKE N'%[^0-9+ -]%' THEN REPLACE(REPLACE(@Term, N' ', N''), N'-', N'') END;

    SELECT   [u].[Id],
             [u].[Email],
             [u].[DisplayName],
             [u].[Phone],
             [u].[Status],
             [u].[Created],
             (SELECT MAX([v].[Level]) FROM [Main].[Verification] AS [v]
              WHERE [v].[UserId] = [u].[Id] AND [v].[Status] = 2 AND [v].[Archived] = 0) AS [VerifiedLevel],
             (SELECT STRING_AGG(CAST([r].[Role] AS VARCHAR (3)), ',') FROM [Main].[UserRole] AS [r]
              WHERE [r].[UserId] = [u].[Id] AND [r].[Archived] = 0) AS [Roles],
             -- A version only while there is a picture: a removed one keeps its timestamp.
             -- The list carries this so the screen knows, per row, whether there is a picture at
             -- all and what version it is. Without it every row asked the avatar endpoint, which
             -- is one request and one query per person just to be told "no picture" — and only
             -- cacheable for a minute, so it happened again on the next visit. Same shape as
             -- QueryStaffMembers and GetAdminUser.
             IIF([p].[AvatarBlob] IS NULL, NULL, [p].[AvatarUpdatedOn]) AS [AvatarUpdatedOn],
             COUNT(1) OVER () AS [TotalCount]
    FROM      [Main].[User]        AS [u]
    LEFT JOIN [Main].[UserProfile] AS [p] ON [p].[UserId] = [u].[Id] AND [p].[Archived] = 0
    WHERE    [u].[Archived] = 0
      AND    (@Status IS NULL OR [u].[Status] = @Status)
      AND    (@Role IS NULL OR EXISTS (SELECT 1 FROM [Main].[UserRole] AS [r]
                                       WHERE [r].[UserId] = [u].[Id] AND [r].[Role] = @Role AND [r].[Archived] = 0))
      AND    (@Term IS NULL
              OR [u].[Email] LIKE LOWER(@Term) + N'%'
              OR (@Digits IS NOT NULL AND LEN(@Digits) >= 4 AND [u].[Phone] LIKE N'%' + RIGHT(@Digits, 10))
              OR [u].[DisplayName] LIKE N'%' + @Term + N'%')
    ORDER BY [u].[Id] DESC
    OFFSET   @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
