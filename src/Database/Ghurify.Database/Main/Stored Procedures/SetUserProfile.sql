-- Saves a user's own profile: the account fields on [Main].[User] and the rest on
-- [Main].[UserProfile], created on first save. One transaction, so a half-saved profile is never
-- visible.
--
-- @Result 0 = saved, 1 = the phone number already belongs to another account (nothing written).
CREATE PROCEDURE [Main].[SetUserProfile]
    @UserId                 BIGINT,
    @DisplayName            NVARCHAR (100),
    @Gender                 TINYINT,
    @Phone                  NVARCHAR (20),
    @Bio                    NVARCHAR (500),
    @HomeDistrict           NVARCHAR (60),
    @EmergencyContactName   NVARCHAR (100),
    @EmergencyContactPhone  NVARCHAR (20),
    @Result                 TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 0;

    BEGIN TRAN;

    IF @Phone IS NOT NULL
       AND EXISTS (SELECT 1
                   FROM   [Main].[User] WITH (UPDLOCK, HOLDLOCK)
                   WHERE  [Phone] = @Phone
                     AND  [Id] <> @UserId)
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[User]
    SET    [DisplayName] = @DisplayName,
           [Gender]      = @Gender,
           [Phone]       = @Phone,
           [UpdatedOn]   = SYSUTCDATETIME(),
           [UpdatedId]   = @UserId
    WHERE  [Id] = @UserId
      AND  [Archived] = 0;

    UPDATE [Main].[UserProfile] WITH (UPDLOCK, HOLDLOCK)
    SET    [Bio]                   = @Bio,
           [HomeDistrict]          = @HomeDistrict,
           [EmergencyContactName]  = @EmergencyContactName,
           [EmergencyContactPhone] = @EmergencyContactPhone,
           [UpdatedOn]             = SYSUTCDATETIME(),
           [UpdatedId]             = @UserId
    WHERE  [UserId] = @UserId;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [Main].[UserProfile]
               ([UserId], [Bio], [HomeDistrict], [EmergencyContactName], [EmergencyContactPhone], [UpdatedId])
        VALUES (@UserId, @Bio, @HomeDistrict, @EmergencyContactName, @EmergencyContactPhone, @UserId);
    END;

    COMMIT TRAN;
END;
