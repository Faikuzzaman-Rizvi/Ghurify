-- The parts of a person's profile that are not needed to sign in: a short bio, where they are
-- from, and who to call in an emergency.
--
-- One row per user, created the first time the profile is saved. Display name, gender and phone
-- stay on [Main].[User], because trips, search and sign-in read them on every request.
CREATE TABLE [Main].[UserProfile]
(
    [Id]                     BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]                 BIGINT          NOT NULL,
    [Bio]                    NVARCHAR (500)  NULL,
    [HomeDistrict]           NVARCHAR (60)   NULL,
    -- The person the safety desk and SOS alerts contact. E.164, e.g. +8801712345678.
    [EmergencyContactName]   NVARCHAR (100)  NULL,
    [EmergencyContactPhone]  NVARCHAR (20)   NULL,
    -- The profile picture: a blob name in the media container (served through short-lived links),
    -- and when it last changed, so browsers can tell a new picture from a cached one.
    [AvatarBlob]             NVARCHAR (200)  NULL,
    [AvatarUpdatedOn]        DATETIME2 (0)   NULL,

    [Archived]               BIT             CONSTRAINT [DF_UserProfile_Archived] DEFAULT ((0)) NOT NULL,
    [Created]                DATETIME2 (0)   CONSTRAINT [DF_UserProfile_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]              DATETIME2 (7)   CONSTRAINT [DF_UserProfile_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]              BIGINT          NULL,

    CONSTRAINT [PK_UserProfile] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserProfile_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_UserProfile_EmergencyContactPhoneE164] CHECK ([EmergencyContactPhone] IS NULL
                                                                 OR ([EmergencyContactPhone] LIKE '+[0-9]%'
                                                                     AND LEN([EmergencyContactPhone]) >= 8))
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_UserProfile_UserId]
    ON [Main].[UserProfile] ([UserId] ASC);
GO
