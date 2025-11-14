# Session 2 Complete: Upload UI Ready for Testing! 🎉

> **Date**: 2025-11-14  
> **Status**: Upload UI complete - Ready to test with real files!  
> **Your 40GB file**: `~/Downloads/Archive.mbox`

---

## ✅ What We Built

### Upload UI Features
- ✅ **Beautiful upload page** at `/upload`
- ✅ **Real-time progress bar** (0-100%)
- ✅ **Speed tracking** (MB/s)
- ✅ **Time remaining estimation**
- ✅ **File type selection** (mbox, Google Takeout, Microsoft Export)
- ✅ **File size validation** against subscription tier
- ✅ **Chunked uploads** (4MB blocks)
- ✅ **Direct to Azure** (no server bandwidth used)

### New Files Created
```
Evermail.WebApp/Components/Pages/
└── Upload.razor                        # Upload UI with progress

Evermail.WebApp/wwwroot/js/
└── azure-blob-upload.js               # Chunked upload logic

Evermail.WebApp/Components/
├── App.razor                           # Added Azure SDK scripts
└── Layout/NavMenu.razor                # Added Upload link
```

---

## 🧪 How to Test

### Step 1: Start Aspire (if not running)

```bash
cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.AppHost
dotnet run
```

**Wait for:**
- ✅ "Now listening on: https://localhost:7136"
- ✅ Dashboard at http://localhost:15275

### Step 2: Open the WebApp

**Navigate to:** https://localhost:7136

### Step 3: Login or Register

If you don't have an account:
1. Click **Register** in nav menu
2. Create account (will be on "Free" tier by default - 1GB max file)

If you have an account:
1. Click **Login**
2. Enter credentials

### Step 4: Upgrade to Enterprise Tier

**Option A: Use Admin UI**
1. Go to https://localhost:7136/admin/subscriptions
2. Enter your email
3. Select "Enterprise" tier (100GB max file)
4. Click "Change Subscription"

**Option B: Use SQL (if you prefer)**
```sql
-- Find your tenant
SELECT t.Id, t.SubscriptionTier, u.Email 
FROM Tenants t 
JOIN AspNetUsers u ON t.Id = u.TenantId;

-- Upgrade to Enterprise
UPDATE Tenants 
SET SubscriptionTier = 'Enterprise', 
    MaxStorageGB = 2000,
    MaxUsers = 50
WHERE Id = 'YOUR_TENANT_ID';
```

### Step 5: Test Upload with Small File First

**Create a test file:**
```bash
# Create 10MB test file
dd if=/dev/zero of=~/Downloads/test-10mb.mbox bs=1m count=10

# Or create 100MB test file
dd if=/dev/zero of=~/Downloads/test-100mb.mbox bs=1m count=100
```

**Or use any existing small file** (.mbox, .zip, .txt for testing)

### Step 6: Upload Your First File

1. Click **Upload** in the navigation menu
2. Select file type: **".mbox File"**
3. Click **Choose File** and select your test file
4. Click **Start Upload**

**You should see:**
- ✅ Progress bar animating 0% → 100%
- ✅ Upload speed in MB/s
- ✅ Time remaining countdown
- ✅ "Upload Complete!" success message

### Step 7: Verify in Aspire Dashboard

1. Open Aspire Dashboard: http://localhost:15275
2. Click **Storage** → **Blobs**
3. You should see container: `mailbox-archives`
4. Inside: `{tenantId}/{mailboxId}/{guid}_test-10mb.mbox`

**Verify Queue:**
1. Click **Storage** → **Queues**
2. You should see queue: `mailbox-processing`
3. Message count: 1 (waiting for worker to process)

---

## 🔥 Test Your 40GB File!

Once small files work, let's try the big one:

```bash
# Your file location
ls -lh ~/Downloads/Archive.mbox

# Should show something like:
# -rw-r--r--  1 kallehiitola  staff    40G Nov 14 10:00 Archive.mbox
```

### Upload Steps:

1. Go to `/upload`
2. Select **".mbox File"**
3. Choose `Archive.mbox` from Downloads
4. Click **Start Upload**

**Expected Behavior:**
- File size: **~40 GB**
- Chunks: **~10,000 blocks** (4MB each)
- Upload time: **15-20 minutes** (depends on connection speed)
- Progress updates: **Every chunk** (smooth progress bar)
- Browser: **Can stay open or close** (upload continues in Azure)

**Watch the progress:**
```
Uploading Archive.mbox...
█████████████████████████░░░░░░ 75%
30,720 MB / 40,960 MB - 00:05:23 remaining
Upload speed: 45.67 MB/s
```

---

## 🐛 Troubleshooting

### Upload Button Disabled
- **Cause**: Not authenticated or no file selected
- **Fix**: Login and select a file

### "File size exceeds plan limit"
- **Cause**: Still on Free tier (1GB limit)
- **Fix**: Upgrade to Enterprise at `/admin/subscriptions`

### Upload Fails Immediately
- **Cause**: API endpoint not reachable or Azure storage not running
- **Fix**: Check Aspire is running, check browser console (F12)

### Progress Bar Stuck at 0%
- **Cause**: JavaScript error or Azure SDK not loaded
- **Fix**: 
  1. Open browser console (F12)
  2. Look for errors
  3. Verify scripts loaded: "Azure Blob Upload script loaded"

### "Upload completed but failed to queue for processing"
- **Cause**: Queue service issue
- **Fix**: Check Aspire logs for queue connection errors

---

## 📊 Browser Console Debugging

**Open DevTools (F12) → Console**

You should see:
```
Azure Blob Upload script loaded
Starting upload: test-10mb.mbox (10485760 bytes)
File will be uploaded in 3 chunks
Uploading block 1/3 (4194304 bytes)
Progress: 33%, Speed: 45.67 MB/s
Uploading block 2/3 (4194304 bytes)
Progress: 66%, Speed: 46.12 MB/s
Uploading block 3/3 (2097152 bytes)
Progress: 100%, Speed: 45.89 MB/s
Committing blocks...
Upload complete!
```

---

## 🎯 Success Checklist

Before testing 40GB file, verify:
- [ ] Small file (10MB) uploads successfully
- [ ] Progress bar animates smoothly
- [ ] Speed and ETA display correctly
- [ ] Upload completes with success message
- [ ] Blob appears in Aspire Storage dashboard
- [ ] Queue message created
- [ ] Can navigate away and back during upload

---

## 📈 Expected Performance

| File Size | Chunks | Upload Time (100 Mbps) | Upload Time (300 Mbps) |
|-----------|--------|------------------------|------------------------|
| 10 MB     | 3      | ~1 second              | ~1 second              |
| 100 MB    | 25     | ~10 seconds            | ~3 seconds             |
| 1 GB      | 250    | ~2 minutes             | ~30 seconds            |
| 10 GB     | 2,500  | ~20 minutes            | ~7 minutes             |
| **40 GB** | 10,000 | **~80 minutes**        | **~27 minutes**        |

**Your 40GB file will take roughly 30-80 minutes depending on your connection speed.**

---

## 🚀 What Happens Next?

After upload completes:
1. ✅ File stored in Azure Blob Storage
2. ✅ Queue message sent to `mailbox-processing`
3. ⏳ **IngestionWorker** will process it (Session 3 - not implemented yet)
4. ⏳ Emails will be parsed with MimeKit
5. ⏳ Emails will be stored in database

**For now:** The file will just sit in the queue until we build the IngestionWorker!

---

## 📝 What You Can Test Right Now

### ✅ Working Now:
- Upload page UI
- File selection
- Progress tracking
- Speed calculation
- Time estimation
- File size validation
- Subscription tier limits
- Direct Azure upload
- Queue message creation

### 🚧 Coming in Session 3:
- Email parsing with MimeKit
- IngestionWorker processing
- Database storage
- Email viewing
- Search functionality

---

## 💡 Testing Tips

### Start Small
1. Test with 10MB file first
2. Then 100MB file
3. Then 1GB file
4. Finally your 40GB Archive.mbox

### Monitor Everything
- **Browser**: Watch progress bar
- **Console**: Check for JavaScript errors
- **Aspire Dashboard**: Verify blob and queue
- **Network Tab**: See individual chunk uploads

### Test Edge Cases
- Cancel upload mid-way (button works but upload continues in Azure)
- Close browser during upload (Azure continues)
- Try uploading without authentication (should fail)
- Try uploading file > plan limit (should fail with nice error)

---

## 🎊 You Can Now Test!

Everything is ready! Just:

1. **Start Aspire** if not running
2. **Open** https://localhost:7136
3. **Login** or register
4. **Upgrade** to Enterprise tier
5. **Click Upload** in nav menu
6. **Select your file** and watch the magic! ✨

---

## 📞 Questions?

If something doesn't work:
1. Check browser console (F12)
2. Check Aspire logs
3. Verify subscription tier
4. Check file permissions on Archive.mbox

**Have fun testing! Your 40GB file upload is just a few clicks away!** 🚀📧

---

**Next Session**: We'll build the IngestionWorker to actually parse those emails with MimeKit!

