# Debugging Evermail in Cursor/VS Code

> **How to debug the Aspire solution in Cursor editor**

---

## ✅ Configuration Complete

Your `.vscode/launch.json` is configured correctly for Aspire debugging!

```json
{
    "type": "aspire",
    "request": "launch",
    "name": "Aspire: Launch Evermail AppHost",
    "program": "${workspaceFolder}/Evermail/Evermail.AppHost/Evermail.AppHost.csproj"
}
```

---

## 🚀 How to Debug

### Method 1: Press F5 (Recommended)

1. **Open Cursor** (make sure you're in the evermail workspace)
2. **Press F5** (or Run → Start Debugging)
3. **Aspire will**:
   - Build all projects
   - Start SQL Server container
   - Start Azurite storage emulator
   - Launch all services (WebApp, AdminApp, Worker)
   - **Open dashboard automatically** in your browser
   - Attach debugger to all .NET projects

### Method 2: Debug Menu

1. Click **Run and Debug** in the sidebar (or Cmd+Shift+D)
2. Select **"Aspire: Launch Evermail AppHost"** from dropdown
3. Click the green **Start Debugging** button

### Method 3: From AppHost File

1. Open `Evermail/Evermail.AppHost/Program.cs`
2. You should see **Run** and **Debug** buttons at the top
3. Click **Debug** button

---

## 🎯 What Happens When You Debug

1. **All projects build**
2. **Docker containers start**:
   - SQL Server (localhost:1433)
   - Azurite (blobs + queues)
3. **Services start with debugger attached**:
   - Evermail.WebApp
   - Evermail.AdminApp
   - Evermail.IngestionWorker
4. **Dashboard opens**: https://localhost:17134
5. **You can now**:
   - Set breakpoints in any project
   - Step through code
   - Inspect variables
   - View console output

---

## 🔍 Setting Breakpoints

**In any .NET file**:
1. Open the file (e.g., `AuthEndpoints.cs`)
2. Click left of line number to set breakpoint (red dot appears)
3. Trigger that code path (e.g., call the API endpoint)
4. Debugger will pause at breakpoint

**Example**:
- Set breakpoint in `AuthEndpoints.cs` at line with `userManager.CreateAsync`
- Call POST /api/v1/auth/register
- Debugger pauses, you can inspect variables

---

## 🛑 Stopping the Debugger

**Method 1**: Press **Shift+F5** (stop debugging)

**Method 2**: Click the red **Stop** button in debug toolbar

**Method 3**: Close the debug terminal

---

## 📊 Debug View Features

When debugging, you'll see:
- **Variables** - All local variables and their values
- **Watch** - Custom expressions to monitor
- **Call Stack** - Function call hierarchy
- **Breakpoints** - All your breakpoints
- **Debug Console** - Execute code while paused

---

## 🐛 Troubleshooting

### "No launch configurations found"

**Solution**: You need the C# Dev Kit extension

```bash
code --install-extension ms-dotnettools.csdevkit
```

Or in Cursor: Extensions → Search "C# Dev Kit" → Install

### Dashboard doesn't open automatically

**Solution**: Open manually while debugging:
- Dashboard URL: https://localhost:17134
- Look for token in Debug Console
- Or use the URL printed in terminal

### Breakpoints not hit

**Check**:
1. Is the service actually running? (Check Aspire Dashboard → Resources)
2. Is your code path being executed? (Add logging to verify)
3. Did you build after setting breakpoint? (Sometimes needs rebuild)

### "Cannot connect to runtime process"

**Solution**:
1. Stop debugging (Shift+F5)
2. Clean: `cd Evermail && dotnet clean`
3. Rebuild: `dotnet build`
4. Try debugging again (F5)

---

## 💡 Pro Tips

### Multi-Project Debugging

**Aspire debugger** automatically attaches to ALL projects in your solution!
- Set breakpoints in WebApp, AdminApp, and Worker simultaneously
- Switch between projects during debug session
- See cross-project call stacks

### Hot Reload

While debugging:
- Edit code
- Save file (Cmd+S)
- Changes apply automatically (no restart needed for many changes)

### Debug Console Commands

While paused at breakpoint, try:
```
user.Email          // Inspect variable
context.Tenants.Count()  // Execute EF query
DateTime.UtcNow    // Evaluate expression
```

---

## 🎯 Quick Start

**Right now**, try this:

1. **Press F5** in Cursor
2. **Wait 30 seconds** (building and starting)
3. **Dashboard should open** automatically
4. **Check Aspire Dashboard** → Resources tab → All green
5. **Set a breakpoint** in `AuthEndpoints.cs`
6. **Call the API** (see TESTING.md)
7. **Debugger pauses** at your breakpoint!

---

**That's it!** Debugging should now work perfectly in Cursor! 🎉

**Try it**: Press F5 and watch everything start up with debugging enabled!

---

**Last Updated**: 2025-11-12  
**Debugger**: Aspire (multi-project)  
**Status**: ✅ Configured and ready

