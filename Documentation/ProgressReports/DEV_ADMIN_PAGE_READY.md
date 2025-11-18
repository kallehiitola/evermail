# ✅ Dev Admin Page Ready!

## 🎯 Super Simple - Just Use Your Browser

**No curl needed! No SSL issues! Just click a button!**

---

## Step 1: Register (if you haven't already)

Go to: https://localhost:7136/register

Register with: `kalle.hiitola@gmail.com`

---

## Step 2: Go to Dev Admin Page

Navigate to: **https://localhost:7136/dev/admin-roles**

---

## Step 3: Click the Big Button

You'll see a green button:

```
🚀 Make kalle.hiitola@gmail.com Admin
```

Click it!

You should see:
```
✅ Successfully added 'kalle.hiitola@gmail.com' to Admin role! 
Current roles: User, Admin
```

---

## Step 4: Test Admin Access

Now go to: **https://localhost:7136/admin/subscriptions**

You should see the **Subscription Manager** page! 🎉

---

## Features of the Dev Admin Page

- ✅ Add Admin role to any user
- ✅ Add SuperAdmin role to any user
- ✅ Check what roles a user has
- ✅ Quick action button for kalle.hiitola@gmail.com
- ✅ Beautiful Bootstrap UI
- ✅ Only works in Development (automatically disabled in production)
- ✅ No SSL/curl issues!

---

## Testing 403 Forbidden

**Test 1: As Regular User (Should Show 403)**
1. Register a different user (e.g., `test@example.com`)
2. Login with that user
3. Try to access: https://localhost:7136/admin/subscriptions
4. **Expected**: Red "Access Denied" page

**Test 2: As Admin (Should Work)**
1. Make yourself admin at `/dev/admin-roles`
2. Access: https://localhost:7136/admin/subscriptions
3. **Expected**: Subscription Manager loads!

---

## 🎊 What You Can Do Now

Once you're an admin:
1. Go to `/admin/subscriptions`
2. Enter any user's email
3. Change their subscription tier (Free → Pro → Team → Enterprise)
4. Test file uploads with different tier limits!

---

## 🚀 Next Steps

**Now that you're an admin:**
1. Upgrade yourself to **Enterprise** tier (100GB max file)
2. Go to `/upload` page
3. Select your `~/Downloads/Archive.mbox` file
4. Watch the progress bar as it uploads! 📊

---

**All sorted! Just visit `/dev/admin-roles` and click the button!** 🎉

