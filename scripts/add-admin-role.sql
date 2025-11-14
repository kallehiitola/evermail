-- Add user kalle.hiitola@gmail.com to Admin role
-- Run this in your SQL Server Management Studio or Azure Data Studio

-- 1. Find the user
DECLARE @UserId UNIQUEIDENTIFIER;
DECLARE @RoleId UNIQUEIDENTIFIER;
DECLARE @UserEmail NVARCHAR(256) = 'kalle.hiitola@gmail.com';

SELECT @UserId = Id 
FROM AspNetUsers 
WHERE Email = @UserEmail OR NormalizedEmail = UPPER(@UserEmail);

-- 2. Find the Admin role
SELECT @RoleId = Id 
FROM AspNetRoles 
WHERE Name = 'Admin';

-- 3. Check if user exists
IF @UserId IS NULL
BEGIN
    PRINT 'ERROR: User not found with email: ' + @UserEmail;
    PRINT 'Please register this user first at https://localhost:7136/register';
END
ELSE IF @RoleId IS NULL
BEGIN
    PRINT 'ERROR: Admin role not found. Database seeding may have failed.';
END
ELSE
BEGIN
    -- 4. Check if user is already in Admin role
    IF EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
    BEGIN
        PRINT 'User is already an Admin!';
    END
    ELSE
    BEGIN
        -- 5. Add user to Admin role
        INSERT INTO AspNetUserRoles (UserId, RoleId)
        VALUES (@UserId, @RoleId);
        
        PRINT 'SUCCESS: User ' + @UserEmail + ' has been added to Admin role!';
    END
    
    -- 6. Show current roles
    PRINT '';
    PRINT 'Current roles for ' + @UserEmail + ':';
    SELECT r.Name as RoleName
    FROM AspNetUserRoles ur
    INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
    WHERE ur.UserId = @UserId;
END

GO

