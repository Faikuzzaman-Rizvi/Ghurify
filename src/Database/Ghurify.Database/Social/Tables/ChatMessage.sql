-- A message in a trip's group chat.
--
-- Members are the host and every traveller whose seat is held or paid; membership is derived from
-- bookings, never stored, so it can never drift from who is actually on the trip.
-- Kind: 1 Message, 2 Announcement (the host's, shown pinned when IsPinned), 3 System.
-- WasMasked records that phone or wallet numbers were hidden in Body because not everyone in the
-- group had paid yet: sharing numbers is how people get talked into paying outside escrow.
CREATE TABLE [Social].[ChatMessage]
(
    [Id]         BIGINT          IDENTITY (1, 1) NOT NULL,
    [TripId]     BIGINT          NOT NULL,
    [SenderId]   BIGINT          NOT NULL,
    [Kind]       TINYINT         CONSTRAINT [DF_ChatMessage_Kind] DEFAULT ((1)) NOT NULL,
    [Body]       NVARCHAR (2000) NOT NULL,
    [IsPinned]   BIT             CONSTRAINT [DF_ChatMessage_IsPinned] DEFAULT ((0)) NOT NULL,
    [WasMasked]  BIT             CONSTRAINT [DF_ChatMessage_WasMasked] DEFAULT ((0)) NOT NULL,

    [Archived]   BIT             CONSTRAINT [DF_ChatMessage_Archived] DEFAULT ((0)) NOT NULL,
    [Created]    DATETIME2 (0)   CONSTRAINT [DF_ChatMessage_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]  DATETIME2 (7)   CONSTRAINT [DF_ChatMessage_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]  BIGINT          NULL,

    CONSTRAINT [PK_ChatMessage] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChatMessage_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [FK_ChatMessage_User_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_ChatMessage_Kind] CHECK ([Kind] >= 1 AND [Kind] <= 3),
    CONSTRAINT [CK_ChatMessage_Body] CHECK (LEN([Body]) > 0)
);
GO

-- History, newest first, a page at a time.
CREATE NONCLUSTERED INDEX [IX_ChatMessage_TripId_Id]
    ON [Social].[ChatMessage] ([TripId] ASC, [Id] DESC)
    INCLUDE ([SenderId], [Kind], [IsPinned])
    WHERE [Archived] = 0;
GO

-- The pinned announcements shown above the conversation.
CREATE NONCLUSTERED INDEX [IX_ChatMessage_TripId_Pinned]
    ON [Social].[ChatMessage] ([TripId] ASC)
    WHERE [IsPinned] = 1 AND [Archived] = 0;
GO
