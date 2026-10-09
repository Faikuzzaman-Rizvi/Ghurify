-- Table-valued parameter for passing a role's permission keys to [Main].[SetStaffRole].
-- The string twin of [Main].[IdList]; never a comma-separated string.
CREATE TYPE [Main].[PermissionList] AS TABLE
(
    [Permission] VARCHAR (40) NOT NULL,
    PRIMARY KEY CLUSTERED ([Permission] ASC)
);
