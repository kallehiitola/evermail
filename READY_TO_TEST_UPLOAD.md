# ✅ READY TO TEST UPLOAD - Final Instructions

## What I Fixed

### ✅ Issue 1: Added Admin Subscriptions Link to Navigation
- **Link appears** in left sidebar: "🔒 Admin: Subscriptions"
- **Only visible** when you have Admin or SuperAdmin role
- **Direct access** to change subscription tiers

### ✅ Issue 2: Fixed Database Persistence  
- **SQL data** now persists in Docker volume: `evermail-sql-data`
- **Blob/queue data** persists in: `evermail-azurite-data`
- **No more data loss** on Aspire restart!
- **Users, roles, uploaded files** all persist

---

## 🚀 Step-by-Step Testing Instructions

### 1. Register/Login

**Go to:** https://localhost:7136

**Register** with `kalle.hiitola@gmail.com` (if not already)

**Or Login** if you already have an account

**Verify:** You should see your email in the top-right corner

---

### 2. Check Authentication Status

**Click:** "Dev: Auth Status" in the left menu

**You should see:**
- ✅ You are logged in!
- Email: kalle.hiitola@gmail.com
- Roles list (probably just "User" for now)

---

### 3. Add Admin Role

**Click:** "Dev: Admin Roles" in the left menu

**Click the green button:** "🚀 Make kalle.hiitola@gmail.com Admin"

**Expected result:**
```
✅ Successfully added 'kalle.hiitola@gmail.com' to Admin role! 
Current roles: User, Admin
```

---

### 4. Verify Admin Link Appears

**Go back to navigation menu**

**You should now see:**
- 🏠 Home
- ☁️ Upload
- ✉️ Emails
- ⚙️ Settings
- 🔒 **Admin: Subscriptions** ← NEW! (only appears after you have Admin role)
- 🔧 Dev: Admin Roles
- 🛡️ Dev: Auth Status

**If you don't see it:** Hard refresh browser (Cmd+Shift+R)

---

### 5. Upgrade to Enterprise Tier

**Click:** "Admin: Subscriptions" in the menu

**Enter:**
- User Email: `kalle.hiitola@gmail.com`
- New Subscription Tier: Select **"Enterprise"**

**Click:** "Change Subscription"

**Expected:**
```
✅ Successfully changed kalle.hiitola@gmail.com from 'Free' to 'Enterprise' tier
```

---

### 6. Test Upload!

**Click:** "Upload" in the menu

**Create a small test file:**
```bash
dd if=/dev/zero of=~/Downloads/test-10mb.mbox bs=1m count=10
```

**On the upload page:**
1. Select file type: ".mbox File"
2. Click "Choose File" → select `test-10mb.mbox`
3. You should see: "Your plan allows up to **100 GB** per file"
4. Click "Start Upload"

**Expected:**
- Progress bar animates 0% → 100%
- Shows upload speed
- Shows time remaining
- "Upload Complete!" message

---

### 7. Test Persistence

**After successful upload:**

1. **Stop Aspire:** Ctrl+C in the terminal where Aspire is running
2. **Restart Aspire:** `cd Evermail.AppHost && dotnet run`
3. **Wait** for startup (~20 seconds)
4. **Login** with kalle.hiitola@gmail.com
5. **Check:**
   - ✅ You still have Admin role (check /dev/auth-status)
   - ✅ You're still on Enterprise tier
   - ✅ No need to re-register or re-add admin role!

---

## 🐛 Troubleshooting

### I don't see "Admin: Subscriptions" link
- **Cause:** You don't have Admin role yet
- **Fix:** Go to `/dev/admin-roles` and add admin role
- **Then:** Hard refresh browser (Cmd+Shift+R)

### Still getting 401 on /admin/subscriptions
- **Cause:** Not logged in
- **Fix:** Check `/dev/auth-status` - if it says "NOT logged in", click Login

### Upload says "1 GB" limit instead of "100 GB"
- **Cause:** Not upgraded to Enterprise tier
- **Fix:** Go to `/admin/subscriptions` and change tier to Enterprise

### Can't find the pages
- **Cause:** Browser cache
- **Fix:** Hard refresh (Cmd+Shift+R) or open in incognito mode

---

## 📊 Navigation Menu Layout

**When NOT logged in:**
- 🏠 Home
- 🔑 Login
- ✍️ Register

**When logged in (regular user):**
- 🏠 Home
- ☁️ Upload
- ✉️ Emails
- ⚙️ Settings
- 🔧 Dev: Admin Roles
- 🛡️ Dev: Auth Status

**When logged in AS ADMIN:**
- 🏠 Home
- ☁️ Upload
- ✉️ Emails
- ⚙️ Settings
- 🔒 **Admin: Subscriptions** ← Appears only for admins!
- 🔧 Dev: Admin Roles
- 🛡️ Dev: Auth Status

---

## ✅ Success Checklist

- [ ] Logged in with kalle.hiitola@gmail.com
- [ ] Added Admin role via `/dev/admin-roles`
- [ ] See "Admin: Subscriptions" link in navigation
- [ ] Can access `/admin/subscriptions` page
- [ ] Upgraded to Enterprise tier (100GB limit)
- [ ] Can access `/upload` page
- [ ] Created 10MB test file
- [ ] Successfully uploaded test file
- [ ] Saw progress bar and completion message
- [ ] Restarted Aspire and data persisted

---

**Once all checkboxes are done, you're ready to upload your 40GB Archive.mbox!** 🎉

---

## 🎯 Next: Test Your 40GB File!

```bash
# Your Archive.mbox location
~/Downloads/Archive.mbox

# Just select it on the upload page and watch it go!
```

**Expected:**
- ~10,000 chunks (4MB each)
- 20-80 minutes upload time
- Real-time progress tracking
- Success! 🎊

