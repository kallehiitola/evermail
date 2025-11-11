# Cursor AI Configuration

This directory contains configuration for Cursor AI editor.

## Important Files

### `.cursorrules` (in project root)
This file contains comprehensive development guidelines for the Evermail project. It should be automatically picked up by Cursor when you open this workspace.

**File location**: `/Users/kallehiitola/Work/evermail/.cursorrules`

### How Cursor Rules Work

Cursor looks for rules files in this order:
1. **Project Rules**: `.cursorrules` in the project root (this is what we use) ✅
2. **User Rules**: `~/.cursor/rules` (global rules for all projects)

### Verifying Rules are Active

To verify that Cursor is using the rules:

1. **Open Cursor Settings**: 
   - Mac: `Cmd + ,`
   - Windows/Linux: `Ctrl + ,`

2. **Navigate to**: Features → Cursor Rules

3. **Check**: You should see "Project Rules" section showing the `.cursorrules` file

4. **If not showing**:
   - Make sure you opened the entire `/Users/kallehiitola/Work/evermail` folder (not a subfolder)
   - Restart Cursor completely (Cmd+Q / Alt+F4, then reopen)
   - Check that `.cursorrules` file exists: `ls -la | grep cursor`

### Forcing Cursor to Reload Rules

If you update the `.cursorrules` file:

1. **Save the file** (Cmd+S / Ctrl+S)
2. **Reload Cursor**: 
   - Open Command Palette (Cmd+Shift+P / Ctrl+Shift+P)
   - Type "Reload Window"
   - Press Enter

### Testing if Rules are Active

Try asking Cursor AI:
```
"Create a new entity class for EmailMessage"
```

If the rules are active, Cursor should:
- Use C# 12+ features (file-scoped namespaces, records)
- Include `TenantId` property (multi-tenancy)
- Follow the entity design pattern from the rules
- Add proper indexes and validation attributes

### Cursor Rules Content

The `.cursorrules` file contains (564 lines):
- ✅ Project overview and tech stack
- ✅ Architecture principles (Clean Architecture, DDD, CQRS)
- ✅ Multi-tenancy patterns
- ✅ C# coding conventions
- ✅ Database design patterns
- ✅ Azure Aspire integration
- ✅ Email processing with MimeKit
- ✅ Security best practices (auth, encryption, GDPR)
- ✅ Blazor and API design patterns
- ✅ Testing strategies
- ✅ Deployment guidelines

## Additional Configuration

### `.cursorignore`
Located in project root. Tells Cursor which files/directories to exclude from AI indexing.

### `.vscode/settings.json`
Contains editor settings for C# development, formatting, and other preferences.

### `.vscode/extensions.json`
Recommended VS Code/Cursor extensions for .NET development.

## Troubleshooting

### Rules not appearing?

1. **Check file exists**:
   ```bash
   ls -la /Users/kallehiitola/Work/evermail/.cursorrules
   ```

2. **Verify workspace folder**:
   - Cursor must be opened at `/Users/kallehiitola/Work/evermail`
   - Not a parent or child folder

3. **Check file permissions**:
   ```bash
   # Should be readable
   cat .cursorrules | head -5
   ```

4. **Restart Cursor**:
   - Completely quit Cursor (Cmd+Q)
   - Reopen the workspace folder

5. **Check Cursor version**:
   - Ensure you're using Cursor 0.30+ (rules support was added in earlier versions)
   - Update if needed: Help → Check for Updates

### Rules seem ignored?

Sometimes Cursor needs explicit reminding. In your prompt, add:
```
"Following the .cursorrules file in this project..."
```

Or reference specific sections:
```
"According to the project's multi-tenancy rules..."
"Following the security best practices in .cursorrules..."
```

## Need Help?

If issues persist:
1. Check Cursor documentation: https://docs.cursor.com
2. Restart Cursor completely
3. Verify the workspace folder is correct
4. Try creating a simple test to verify rules are active

---

**Last Updated**: 2025-11-11

