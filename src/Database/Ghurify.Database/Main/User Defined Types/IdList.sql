-- Table-valued parameter for passing a list of ids to a procedure.
-- Always use this instead of a comma-separated string.
CREATE TYPE [Main].[IdList] AS TABLE
(
    [Id] BIGINT NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);
