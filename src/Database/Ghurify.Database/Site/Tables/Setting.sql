-- What the super admin has changed about the site: its name, tagline, contact details, social
-- links, colours and fonts.
--
-- Only rows that differ from the shipped default are here. A key with no row uses its default
-- from Ghurify.Domain.Site.SiteSettingsCatalog, so an untouched database renders exactly the
-- site the code shipped with, and "reset" means deleting the row rather than storing a copy of
-- the default.
--
-- Value is plain text, not JSON. Every setting is a single scalar — a name, a hex colour, a URL,
-- a font family — and the code's catalogue already says which kind each key is, so a JSON
-- wrapper would add a parse step and a class of malformed-value failures without buying
-- anything. A setting that genuinely needs structure gets its own table.
CREATE TABLE [Site].[Setting]
(
    [Id]         BIGINT          IDENTITY (1, 1) NOT NULL,
    -- Dotted path, e.g. site.name, theme.colour.hill.
    [Key]        VARCHAR (60)    NOT NULL,
    [Value]      NVARCHAR (400)  NOT NULL,

    [Archived]   BIT             CONSTRAINT [DF_Setting_Archived] DEFAULT ((0)) NOT NULL,
    [Created]    DATETIME2 (0)   CONSTRAINT [DF_Setting_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]  DATETIME2 (7)   CONSTRAINT [DF_Setting_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]  BIGINT          NULL,

    CONSTRAINT [PK_Setting] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CK_Setting_Key] CHECK (NOT [Key] LIKE '%[^a-z0-9.-]%' COLLATE Latin1_General_CS_AS
                                       AND LEN([Key]) > 0)
);
GO

-- One live value per key. Archived rows are the trail of what it used to be.
CREATE UNIQUE NONCLUSTERED INDEX [UX_Setting_Key]
    ON [Site].[Setting] ([Key] ASC)
    WHERE [Archived] = 0;
GO
