# ✅ Working Test Guide - Ready to Test Upload!

## Step-by-Step Testing Instructions

### 1️⃣ Aspire is Running

Aspire should be running in the background now. Check the Aspire dashboard:

**Open:** http://localhost:15275

You should see:
- ✅ webapp (running)
- ✅ adminapp
- ✅ worker
- ✅ sql
- ✅ storage

---

### 2️⃣ Register/Login

**Open:** https://localhost:7136

**Register** with: `kalle.hiitola@gmail.com` (if not already done)

Or **Login** if you already have an account.

---

### 3️⃣ Make Yourself Admin

**Click** "Dev: Admin Roles" in the left navigation menu

**Or navigate directly to:** https://localhost:7136/dev/admin-roles

You should see a page with three cards:
- Add Admin Role
- Add SuperAdmin Role  
- Check User Roles
- Quick Actions (green button)

**Click the green button:** "🚀 Make kalle.hiitola@gmail.com Admin"

**Expected Result:**
```
✅ Successfully added 'kalle.hiitola@gmail.com' to Admin role! 
Current roles: User, Admin
```

---

### 4️⃣ Test 403 Forbidden (Optional)

**Test that authorization works:**

1. Logout (top-right corner)
2. Register a different user (e.g., `test@example.com`)
3. Login with that test user
4. Try to go to: https://localhost:7136/admin/subscriptions
5. **Expected:** Red "Access Denied" page with 🛡️ icon

---

### 5️⃣ Upgrade to Enterprise Tier

**Login as kalle.hiitola@gmail.com**

**Go to:** https://localhost:7136/admin/subscriptions

**Enter:**
- User Email: `kalle.hiitola@gmail.com`
- New Subscription Tier: `Enterprise`

**Click:** "Change Subscription"

**Expected:**
```
✅ Successfully changed kalle.hiitola@gmail.com from 'Free' to 'Enterprise' tier
```

---

### 6️⃣ Test Upload with Small File

**Click "Upload" in the navigation menu**

**Or go to:** https://localhost:7136/upload

**Create a test file:**
```bash
# Create 10MB test file
dd if=/dev/zero of=~/Downloads/test-10mb.mbox bs=1m count=10
```

**On the upload page:**
1. Select file type: ".mbox File" (should be selected by default)
2. Click "Choose File"
3. Select `test-10mb.mbox`
4. You should see: "Selected: test-10mb.mbox, Size: 10.00 MB (0.01 GB)"
5. Click "Start Upload"

**Expected:**
- ✅ Progress bar animates 0% → 100%
- ✅ Shows upload speed (MB/s)
- ✅ Shows time remaining
- ✅ "Upload Complete!" message
- ✅ Can click "View Mailbox Status"

**Verify in browser console (F12):**
```
Azure Blob Upload script loaded
Starting upload: test-10mb.mbox (10485760 bytes)
File will be uploaded in 3 chunks
Uploading block 1/3...
Progress: 33%, Speed: XX.XX MB/s
...
Upload complete!
```

---

### 7️⃣ Verify in Aspire Dashboard

**Open:** http://localhost:15275

**Click:** Storage → Blobs

**You should see:**
- Container: `mailbox-archives`
- Inside: `{tenantId}/{mailboxId}/{guid}_test-10mb.mbox`

**Click:** Storage → Queues

**You should see:**
- Queue: `mailbox-processing`
- Message count: 1

---

### 8️⃣ Test Your 40GB File (When Ready)

Once small files work, test with:

```bash
ls -lh ~/Downloads/Archive.mbox
```

**On upload page:**
1. Select ".mbox File"
2. Choose `Archive.mbox`
3. You should see: "Selected: Archive.mbox, Size: ~40000 MB (~40 GB)"
4. Click "Start Upload"

**Expected:**
- Upload will take 20-80 minutes (depending on connection)
- ~10,000 chunks uploaded
- Real-time progress tracking
- Can close browser (upload continues)

---

## 🐛 If Something Doesn't Work

### Page shows 404
- **Restart Aspire** (stop and start again)
- **Hard refresh** browser (Cmd+Shift+R)

### Upload button disabled
- **Check** you're logged in
- **Check** you selected a file

### "File size exceeds plan limit"
- **Verify** you upgraded to Enterprise at `/admin/subscriptions`
- **Check** it says "Your plan allows up to 100 GB per file"

### Progress bar stuck at 0%
- **Open browser console** (F12) → Look for errors
- **Check** "Azure Blob Upload script loaded" message appears

---

## ✅ Summary - URLs to Test

1. **Dev Admin Page:** https://localhost:7136/dev/admin-roles
2. **Admin Subscription Manager:** https://localhost:7136/admin/subscriptions
3. **Upload Page:** https://localhost:7136/upload
4. **Aspire Dashboard:** http://localhost:15275

---

**Everything is ready! Just follow the steps above!** 🚀

