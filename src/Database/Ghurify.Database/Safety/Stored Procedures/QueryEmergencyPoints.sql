-- Police stations and hospitals, for the safety desk to keep right: all of them, or one
-- destination's, with whether a person has checked the details and when.
CREATE PROCEDURE [Safety].[QueryEmergencyPoints]
    @DestinationSlug VARCHAR (60) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [e].[Id],
             [d].[Slug]              AS [DestinationSlug],
             [d].[Name]              AS [DestinationName],
             [e].[Kind],
             [e].[Name],
             [e].[NameBn],
             [e].[Phone],
             [e].[Location].[Lat]    AS [Latitude],
             [e].[Location].[Long]   AS [Longitude],
             [e].[CheckedOn],
             [c].[DisplayName]       AS [CheckedBy]
    FROM     [Safety].[EmergencyPoint] AS [e]
    LEFT JOIN [Main].[Destination]     AS [d] ON [d].[Id] = [e].[DestinationId]
    LEFT JOIN [Main].[User]            AS [c] ON [c].[Id] = [e].[CheckedById]
    WHERE    [e].[Archived] = 0
      AND    (@DestinationSlug IS NULL OR [d].[Slug] = @DestinationSlug)
    ORDER BY [d].[Name], [e].[Kind], [e].[Name];
END;
