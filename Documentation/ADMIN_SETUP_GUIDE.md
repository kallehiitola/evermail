# Admin Role Setup Guide

## 🎯 Quick Start: Add Admin Role to kalle.hiitola@gmail.com

### Option 1: Use Dev Admin Page (Easiest) ✅

**Prerequisites:**
- Aspire is running
- User `kalle.hiitola@gmail.com` is registered

**Step 1: Open the Dev Admin Page**

Navigate to: https://localhost:7136/dev/admin-roles

**Step 2: Make yourself Admin**

Click the big button: **"🚀 Make kalle.hiitola@gmail.com Admin"**

You should see:
```
✅ Successfully added 'kalle.hiitola@gmail.com' to Admin role! 
Current roles: User, Admin
```

**Step 3: Test Admin Access**

Now navigate to: https://localhost:7136/admin/subscriptions

You should see the Subscription Manager page! 🎉

---

### Option 2: SQL Script (Alternative)

If you prefer SQL, run the script:

```sql
-- File: scripts/add-admin-role.sql
-- Run in Azure Data Studio or SQL Server Management Studio

-- Change this email to your user
DECLARE @UserEmail NVARCHAR(256) = 'kalle.hiitola@gmail.com';

-- ... rest of script (see scripts/add-admin-role.sql)
```

---

## 🔒 Authorization Testing

### Test 1: Without Admin Role (Should Show 403)

1. Register a new user (e.g., `test@example.com`)
2. Login with that user
3. Try to access: https://localhost:7136/admin/subscriptions
4. **Expected**: Beautiful "Access Denied" page with red card

### Test 2: With Admin Role (Should Work)

1. Login as `kalle.hiitola@gmail.com` (after adding admin role)
2. Access: https://localhost:7136/admin/subscriptions
3. **Expected**: Subscription Manager page loads successfully

### Test 3: Check Logs

When unauthorized user tries to access admin page:
```
⛔ 403 - Unauthorized access attempt: https://localhost:7136/admin/subscriptions
```

---

## 📋 Available Roles

| Role | Description | Default Users |
|------|-------------|---------------|
| **User** | Regular user (everyone gets this on registration) | All |
| **Admin** | Can manage subscriptions, view admin pages | None (you assign) |
| **SuperAdmin** | Full system access (future use) | None (you assign) |

---

## 🛠️ Development Endpoints Reference

All dev endpoints are **only available in Development environment**.

### Add Admin Role
```bash
POST /api/v1/dev/add-admin?email={email}
```

### Add SuperAdmin Role
```bash
POST /api/v1/dev/add-superadmin?email={email}
```

### Get User Roles
```bash
GET /api/v1/dev/user-roles/{email}
```

**Example:**
```bash
# Add admin
curl -X POST "https://localhost:7136/api/v1/dev/add-admin?email=kalle.hiitola%40gmail.com"

# Check roles
curl "https://localhost:7136/api/v1/dev/user-roles/kalle.hiitola@gmail.com"

# Response:
{
  "success": true,
  "data": {
    "email": "kalle.hiitola@gmail.com",
    "userId": "guid-here",
    "tenantId": "guid-here",
    "roles": ["User", "Admin"]
  }
}
```

---

## ✨ 403 Forbidden Page

When a non-admin user tries to access admin pages, they see:

```
┌─────────────────────────────────────┐
│ 🛡️ Access Denied                    │
├─────────────────────────────────────┤
│ You don't have permission to        │
│ access this page.                   │
│                                     │
│ This page requires administrator    │
│ privileges. If you believe you      │
│ should have access, please contact  │
│ your system administrator.          │
│                                     │
│ [🏠 Go to Home]                     │
└─────────────────────────────────────┘
```

This is logged as:
```
⛔ 403 - Unauthorized access attempt: {Path}
```

---

## 🧪 Testing Checklist

- [ ] Register user `kalle.hiitola@gmail.com` (if not already done)
- [ ] Use dev endpoint to add Admin role
- [ ] Verify roles with `/user-roles` endpoint
- [ ] Login as that user
- [ ] Access `/admin/subscriptions` - should work
- [ ] Logout
- [ ] Login as different user (non-admin)
- [ ] Try to access `/admin/subscriptions` - should show 403
- [ ] Check Aspire logs for warning message

---

## 🚀 Quick Commands

```bash
# 1. Start Aspire (if not running)
cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.AppHost
dotnet run

# 2. In another terminal, add admin role
curl -X POST "https://localhost:7136/api/v1/dev/add-admin?email=kalle.hiitola%40gmail.com"

# 3. Verify
curl "https://localhost:7136/api/v1/dev/user-roles/kalle.hiitola@gmail.com"

# 4. Open browser
open https://localhost:7136/admin/subscriptions
```

---

## 🔐 Security Notes

**Development Endpoints:**
- Only enabled when `ASPNETCORE_ENVIRONMENT=Development`
- Automatically disabled in Production
- No authentication required (dev only)

**Production:**
- Dev endpoints return 404 in production
- Admin roles must be assigned through secure processes
- Consider implementing admin invitation system

**Best Practices:**
- Don't commit admin credentials to git
- Use environment-specific role seeding
- Implement proper admin invitation flow for production
- Add audit logging for role changes

---

## 📖 Related Files

- `Evermail.WebApp/Endpoints/DevEndpoints.cs` - Development endpoints
- `scripts/add-admin-role.sql` - SQL script for role assignment
- `Evermail.WebApp/Components/Routes.razor` - 403 handling
- `Evermail.WebApp/Components/Pages/Admin/SubscriptionManager.razor` - Admin page

---

**Ready to test!** Run the curl command above and you should be able to access the admin panel. 🎉

