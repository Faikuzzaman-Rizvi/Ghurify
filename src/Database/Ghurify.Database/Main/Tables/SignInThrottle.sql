-- Wrong-password counts per email address, for pausing sign-in after repeated failures.
--
-- Keyed on a keyed hash of the address (HMAC with a server-side secret), not on the account:
-- addresses with no account are counted and paused exactly like real ones, so the pause cannot
-- be used to find out who is registered. The address itself is never stored here.
--
-- A row is deleted on a successful sign-in or a password reset.
CREATE TABLE [Main].[SignInThrottle]
(
    [Id]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [EmailHash]       VARBINARY (32)  NOT NULL,
    [Failures]        TINYINT         CONSTRAINT [DF_SignInThrottle_Failures] DEFAULT ((0)) NOT NULL,
    -- When the current run of failures began. A run older than the window starts again at one.
    [WindowStart]     DATETIME2 (0)   NOT NULL,
    [LockedUntil]     DATETIME2 (0)   NULL,

    [Archived]        BIT             CONSTRAINT [DF_SignInThrottle_Archived] DEFAULT ((0)) NOT NULL,
    [Created]         DATETIME2 (0)   CONSTRAINT [DF_SignInThrottle_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]       DATETIME2 (7)   CONSTRAINT [DF_SignInThrottle_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]       BIGINT          NULL,

    CONSTRAINT [PK_SignInThrottle] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_SignInThrottle_EmailHash]
    ON [Main].[SignInThrottle] ([EmailHash] ASC)
    INCLUDE ([Failures], [WindowStart], [LockedUntil]);
GO
