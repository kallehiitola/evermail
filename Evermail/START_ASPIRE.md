# How to Start Aspire - Simple Instructions

## ✅ Everything is Stopped and Clean Now

Run these commands in **YOUR terminal** (not through AI):

## 📋 Commands to Run

```bash
# 1. Set up .NET 9
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"

# 2. Go to AppHost folder
cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.AppHost

# 3. Run Aspire
dotnet run
```

## 🎯 What Will Happen

You'll see console output like:

```
Building...
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.0.0+...
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application started. Press Ctrl+C to shut down.
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at https://localhost:17134/login?t=abc123...
                                     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                                     THIS IS THE FULL URL WITH TOKEN
```

## 🔗 How to Use the Token

**Option 1 (Easiest)**: 
- Hold `Cmd` key
- Click the URL in the terminal (the one that says "Login to the dashboard at...")
- Safari opens and logs you in automatically

**Option 2**: 
- Copy the ENTIRE URL (including `?t=...`)
- Paste in Safari
- Press Enter

**DON'T**: Try to copy just the token - copy the whole URL!

## 🛑 To Stop Aspire

**Press**: `Ctrl + C` in the terminal where it's running

(That's the proper way, not killing processes!)

---

**That's it!** Just 3 commands, token prints automatically, Cmd+click to login. Simple! 🚀

