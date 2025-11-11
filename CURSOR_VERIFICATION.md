# Cursor AI Rules Verification Checklist

This document helps you verify that Cursor is properly using the modern `.cursor/rules/` format.

## ✅ Setup Verification

### 1. File Structure Check

Run this command to verify all files are in place:

```bash
cd /Users/kallehiitola/Work/evermail
ls -la | grep -E "cursor|vscode|AGENTS"
```

**Expected output**:
- `AGENTS.md` (high-level project instructions)
- `.cursorignore`
- `.cursor/` directory with:
  - `rules/` subdirectory containing 8 `.mdc` files
  - `README.md` documentation
- `.vscode/` directory

### 2. Verify Rule Files

```bash
ls -la .cursor/rules/
```

**Expected output** (8 MDC files):
- `multi-tenancy.mdc` (~180 lines)
- `csharp-standards.mdc` (~220 lines)
- `database-patterns.mdc` (~270 lines)
- `security.mdc` (~350 lines)
- `azure-aspire.mdc` (~200 lines)
- `email-processing.mdc` (~280 lines)
- `api-design.mdc` (~300 lines)
- `blazor-frontend.mdc` (~260 lines)

### 2. Workspace Verification

**IMPORTANT**: Make sure Cursor is opened at the correct level!

✅ **Correct**: Open `/Users/kallehiitola/Work/evermail`
❌ **Wrong**: Open `/Users/kallehiitola/Work` (parent)
❌ **Wrong**: Open `/Users/kallehiitola/Work/evermail/Documentation` (subfolder)

### 3. Cursor Settings Check

1. Open Cursor
2. Press `Cmd + ,` (macOS) or `Ctrl + ,` (Windows/Linux)
3. Navigate to: **Features → Cursor Rules** or search "rules"
4. You should see:
   - **Project Rules** section listing all 8 `.mdc` files
   - **AGENTS.md** showing in project context
   - Each rule showing its description and scope (globs)
   - "Always Apply" badge on `multi-tenancy.mdc` and `security.mdc`

## 🧪 Testing the Rules

### Test 1: Entity Creation

Ask Cursor AI:
```
"Create a new C# entity class for a Workspace that follows the project's multi-tenancy pattern"
```

**Expected behavior** (rules are active):
- ✅ Uses file-scoped namespace
- ✅ Includes `TenantId` property with `[Required, MaxLength(64)]`
- ✅ Includes `CreatedAt`, `UpdatedAt` timestamps
- ✅ Follows naming conventions (PascalCase)
- ✅ Adds proper indexes
- ✅ Includes navigation properties

**If NOT following rules**:
- ❌ Uses old-style namespace with braces
- ❌ Missing TenantId
- ❌ No validation attributes

### Test 2: API Endpoint Creation

Ask Cursor AI:
```
"Create a minimal API endpoint for searching emails with tenant isolation"
```

**Expected behavior** (rules are active):
- ✅ Filters by `TenantId` from context
- ✅ Uses async/await with `Async` suffix
- ✅ Returns `ApiResponse<T>` pattern
- ✅ Includes pagination
- ✅ Uses `AsNoTracking()` for read queries
- ✅ Proper error handling

### Test 3: Security Pattern

Ask Cursor AI:
```
"Show me how to implement the authentication middleware according to project standards"
```

**Expected behavior** (rules are active):
- ✅ Mentions JWT with ES256 algorithm
- ✅ Includes TenantId in claims
- ✅ Uses TenantContext resolver
- ✅ References ASP.NET Identity
- ✅ Mentions 2FA support

## 🔧 Troubleshooting

### Rules not appearing in Cursor Settings?

1. **Completely restart Cursor**:
   ```bash
   # Quit Cursor completely (not just close window)
   # macOS: Cmd+Q
   # Windows: Alt+F4
   # Then reopen
   ```

2. **Verify file is readable**:
   ```bash
   head -10 .cursorrules
   ```

3. **Check file size**:
   ```bash
   ls -lh .cursorrules
   # Should show ~17KB
   ```

4. **Verify not in .gitignore**:
   ```bash
   git check-ignore .cursorrules
   # Should return nothing (file is tracked)
   ```

5. **Check Cursor version**:
   - Open Cursor → Help → About
   - Ensure version 0.30+
   - Update if needed

### Rules seem ignored by AI?

Try explicitly referencing them in your prompts:

```
"Following the .cursorrules guidelines in this project, create..."
"According to the project's multi-tenancy rules..."
"Using the security patterns from .cursorrules..."
```

### Still not working?

1. **Reload window**:
   - `Cmd+Shift+P` / `Ctrl+Shift+P`
   - Type "Reload Window"
   - Press Enter

2. **Clear Cursor cache**:
   ```bash
   # Close Cursor first, then:
   # macOS:
   rm -rf ~/Library/Application\ Support/Cursor/Cache/*
   
   # Linux:
   rm -rf ~/.config/Cursor/Cache/*
   
   # Windows:
   # Delete: %APPDATA%\Cursor\Cache\*
   ```

3. **Check Cursor logs**:
   - Help → Toggle Developer Tools
   - Check Console tab for errors related to rules

## 📚 Rules Content Summary

Your rules are now organized in **8 focused MDC files** (~2,060 total lines):

### Rule Files:
1. **multi-tenancy.mdc** (Always Apply) - TenantId enforcement, isolation
2. **csharp-standards.mdc** (C# files) - C# 12+, naming, async patterns
3. **database-patterns.mdc** (Data files) - EF Core, migrations, FTS
4. **security.mdc** (Always Apply) - Auth, encryption, GDPR
5. **azure-aspire.mdc** (AppHost files) - Service discovery, deployment
6. **email-processing.mdc** (Email services) - MimeKit, mbox parsing
7. **api-design.mdc** (API files) - REST, versioning, OpenAPI
8. **blazor-frontend.mdc** (Razor files) - Components, state, MudBlazor

Plus **AGENTS.md** with high-level project context (architecture, tech stack, business model)

## ✨ Success Indicators

You'll know the rules are working when Cursor:
- ✅ Always includes `TenantId` in entities
- ✅ Uses file-scoped namespaces automatically
- ✅ Suggests proper async patterns
- ✅ References Azure Aspire components
- ✅ Applies security best practices
- ✅ Follows the documented architecture

## 📞 Need Help?

If you continue having issues:
1. Check [Cursor Documentation](https://docs.cursor.com)
2. Verify workspace folder is correct
3. Try the test prompts above
4. Check `.cursor/README.md` for additional troubleshooting

---

**File Locations**:
- Rules: `/Users/kallehiitola/Work/evermail/.cursor/rules/*.mdc`
- Agents: `/Users/kallehiitola/Work/evermail/AGENTS.md`
- Config: `/Users/kallehiitola/Work/evermail/.cursor/`
- Ignore: `/Users/kallehiitola/Work/evermail/.cursorignore`

**Format**: Modern Cursor MDC rules (v2024+)  
**Old Format**: `.cursorrules` has been deprecated and renamed to `.cursorrules.deprecated`  
**Last Updated**: 2025-11-11

