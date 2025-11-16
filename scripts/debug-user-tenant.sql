-- Debug script to check user and tenant data

-- Check if user exists
SELECT 
    'User Exists' as Check_Type,
    Id as User_Id,
    Email,
    TenantId,
    FirstName,
    LastName
FROM AspNetUsers
WHERE Email = 'kalle.hiitola@gmail.com';

-- Check tenant
SELECT
    'Tenant Exists' as Check_Type,
    Id as Tenant_Id,
    Name,
    SubscriptionTier,
    MaxStorageGB
FROM Tenants;

-- Check user roles
SELECT 
    'User Roles' as Check_Type,
    u.Email,
    r.Name as RoleName
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r ON ur.RoleId = r.RoleId
WHERE u.Email = 'kalle.hiitola@gmail.com';

-- Check mailboxes
SELECT
    'Mailboxes' as Check_Type,
    Id,
    FileName,
    Status,
    TenantId,
    UserId
FROM Mailboxes;

