# Cursor AI Configuration

This directory contains Cursor AI rules configuration in the modern MDC format.

## 📁 Project Rules Structure

Evermail uses Cursor's modern `.cursor/rules/` directory with focused, composable rule files:

```
.cursor/rules/
├── documentation.mdc          # CRITICAL - Always applied (Doc-driven dev)
├── multi-tenancy.mdc         # CRITICAL - Always applied
├── security.mdc               # CRITICAL - Always applied
├── csharp-standards.mdc       # C# 12+ conventions
├── database-patterns.mdc      # EF Core patterns
├── azure-aspire.mdc           # Aspire integration
├── email-processing.mdc       # MimeKit patterns
├── api-design.mdc             # REST conventions
├── blazor-frontend.mdc        # Blazor components
└── development-workflow.mdc   # Dev standards & practices
```

Plus `AGENTS.md` in project root for high-level project context.

## 🎯 Rule Types

### 1. Always Apply Rules
- **documentation.mdc** - Document-driven development (check docs FIRST)
- **multi-tenancy.mdc** - Multi-tenant patterns (TenantId enforcement)
- **security.mdc** - Security patterns (auth, encryption, GDPR)

### 2. File-Scoped Rules
Automatically applied when working with matching files:
- `csharp-standards.mdc` → `**/*.cs`
- `database-patterns.mdc` → `**/Data/**/*.cs`, `**/Entities/**/*.cs`
- `azure-aspire.mdc` → `**/AppHost/**/*.cs`, `**/*Program.cs`
- `email-processing.mdc` → `**/Services/*Email*.cs`, `**/Workers/*Ingestion*.cs`
- `api-design.mdc` → `**/Controllers/**/*.cs`, `**/Endpoints/**/*.cs`
- `blazor-frontend.mdc` → `**/*.razor`

### 3. AGENTS.md
High-level project instructions in simple markdown format (project root).

## ✅ Verifying Rules are Active

### Check in Cursor Settings

1. Open Cursor Settings: `Cmd/Ctrl + ,`
2. Navigate to: **Features → Cursor Rules**
3. You should see:
   - **Project Rules** section listing all `.mdc` files
   - **AGENTS.md** shown in project context
   - Rule descriptions and when they apply

### Test with AI

Ask Cursor to create something:
```
"Create a new entity class for a Workspace"
```

**Expected** (rules active):
- ✅ File-scoped namespace
- ✅ `TenantId` property (multi-tenancy rule)
- ✅ `CreatedAt`, `UpdatedAt` timestamps
- ✅ Proper validation attributes
- ✅ Index configuration

## 📚 Rule Content Summary

| Rule File | Lines | Description | Always Apply |
|-----------|-------|-------------|--------------|
| `documentation.mdc` | ~380 | Document-driven development | ✅ Yes |
| `multi-tenancy.mdc` | ~180 | Multi-tenant patterns (CRITICAL) | ✅ Yes |
| `security.mdc` | ~350 | Auth, encryption, GDPR | ✅ Yes |
| `csharp-standards.mdc` | ~220 | C# 12+ conventions | ❌ `**/*.cs` |
| `database-patterns.mdc` | ~270 | EF Core patterns | ❌ Data files |
| `azure-aspire.mdc` | ~200 | Aspire integration | ❌ AppHost files |
| `email-processing.mdc` | ~280 | MimeKit patterns | ❌ Email services |
| `api-design.mdc` | ~300 | REST API patterns | ❌ API files |
| `blazor-frontend.mdc` | ~260 | Blazor components | ❌ `.razor` files |
| `development-workflow.mdc` | ~350 | Dev standards & practices | ❌ General |

**Total**: ~2,790 lines across 10 focused files (each under 400 lines)

## 🆚 Old vs New Format

| Feature | Old (.cursorrules) | New (.cursor/rules/) |
|---------|-------------------|----------------------|
| Format | Single file | Multiple MDC files |
| Size | 564 lines (all in one) | 8 focused files |
| Scope | Global | File-specific globs |
| Metadata | None | Description, globs, alwaysApply |
| Composability | No | Yes |
| Performance | Load everything | Load what's needed |

## 🔧 Forcing Cursor to Reload Rules

If you update rule files:

1. **Save the file** (`Cmd/Ctrl + S`)
2. **Reload Window**: 
   - Command Palette (`Cmd/Ctrl + Shift + P`)
   - Type "Reload Window"
   - Press Enter

Or completely restart Cursor (`Cmd + Q` / `Alt + F4`).

## 📖 MDC Format Example

```mdc
---
description: Brief description of what this rule does
globs: ["**/*.cs", "**/*.razor"]
alwaysApply: false
---

# Rule Title

Rule content in markdown format with code examples...
```

## 🎨 Cursor Features

### Agent Sidebar
Active rules show in the Agent sidebar when chatting. You'll see which rules are being applied to the current context.

### @-Mention Rules
You can explicitly reference rules:
```
"@multi-tenancy create a new entity"
```

### Rule Intelligence
Cursor automatically applies relevant rules based on:
- Files you're editing
- `globs` patterns in MDC metadata
- `alwaysApply` flag
- AI determination of relevance

## 🐛 Troubleshooting

### Rules not appearing?

1. **Verify workspace folder**:
   ```bash
   pwd
   # Should be: /Users/kallehiitola/Work/evermail
   ```

2. **Check rule files exist**:
   ```bash
   ls -la .cursor/rules/
   # Should list 8 .mdc files
   ```

3. **Verify AGENTS.md**:
   ```bash
   ls -la AGENTS.md
   # Should exist in project root
   ```

4. **Restart Cursor completely**:
   - Quit (`Cmd + Q` / `Alt + F4`)
   - Reopen workspace

### Rules seem ignored?

- Try explicitly mentioning them: `"Following the multi-tenancy rules..."`
- Check that you're editing files matching the `globs` patterns
- Verify in Settings that rules are enabled

### Old .cursorrules file

The old `.cursorrules` file has been renamed to `.cursorrules.deprecated` and is no longer used. All rules have been migrated to the modern `.cursor/rules/` format.

## 📞 Need Help?

- Check Cursor Docs: https://docs.cursor.com/context/rules
- Review `CURSOR_VERIFICATION.md` for testing procedures
- Check `AGENTS.md` for high-level project context

---

**Last Updated**: 2025-11-11  
**Format**: Modern Cursor MDC rules (v2024+)
