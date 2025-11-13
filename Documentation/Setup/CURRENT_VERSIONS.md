# Evermail - Current Tool Versions

> **Last Updated**: 2025-11-11  
> **All tools verified and up to date**

---

## ✅ Development Tools (Latest Versions)

| Tool | Current Version | Status | Check Command |
|------|----------------|--------|---------------|
| **.NET SDK** | **10.0.100 LTS** | ✅ Latest | `dotnet --version` |
| **C# Language** | **C# 14** | ✅ With .NET 10 | - |
| **Aspire Templates** | 13.0.0 | ✅ Latest | `dotnet new list aspire` |
| **Azure CLI** | 2.79.0 | ✅ Latest | `az version` |
| **Azure Developer CLI** | 1.21.1 | ✅ Latest | `azd version` |
| **Docker Desktop** | (check manually) | ⚙️ Required | `docker --version` |

---

## 📦 Aspire Package Versions

| Package | Version | Project |
|---------|---------|---------|
| **Aspire.AppHost.Sdk** | 13.0.0 | Evermail.AppHost |
| **Aspire.Hosting.AppHost** | 13.0.0 | Evermail.AppHost |
| **Aspire.Hosting.Azure.Sql** | 13.0.0 | Evermail.AppHost |
| **Aspire.Hosting.Azure.Storage** | 13.0.0 | Evermail.AppHost |
| **Microsoft.Extensions.ServiceDiscovery** | 10.0.0 | Evermail.ServiceDefaults |
| **OpenTelemetry.*** | 1.10.0 | Evermail.ServiceDefaults |

---

## 🔄 Update Schedule

### Weekly Check (Recommended)

Every Monday, check for updates:
```bash
# Quick version check
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
echo ".NET SDK:" && dotnet --version
echo "Azure CLI:" && az version --output json | grep '"azure-cli"'
echo "azd:" && azd version
echo "Aspire:" && dotnet workload list
```

### Monthly Updates (Recommended)

First week of each month:
```bash
# Update all tools (WITH USER APPROVAL FIRST!)
dotnet workload update
az upgrade  
brew upgrade azd
dotnet new install Aspire.ProjectTemplates --force
```

---

## ⚠️ Update Policy

### AI Agent Rules

**When checking versions**:
1. ✅ Check current versions
2. ✅ Compare with latest available
3. ✅ **ASK USER** before updating
4. ✅ Explain what will be updated
5. ✅ Wait for explicit approval

**NEVER**:
- ❌ Auto-update without asking
- ❌ Update during active development
- ❌ Update before deployment
- ❌ Update multiple tools at once without testing

### User Approval Required

**Example Dialog**:
```
AI: "I notice these updates are available:
- Aspire: 13.0.0 → 13.1.0 (new features, bug fixes)
- Azure CLI: 2.79.0 → 2.80.0 (security patches)

Would you like me to update them?
I'll update one at a time and test the build after each."

User: "Yes, go ahead"

AI: [Updates Aspire, tests build, updates Azure CLI, confirms success]
```

---

## 📊 Version Tracking

### Where Versions Are Documented

Update these files when versions change:

1. **CURRENT_VERSIONS.md** (this file) - Tool versions
2. **Evermail/README.md** - Solution version info
3. **PHASE0_COMPLETE.md** - Phase completion with versions
4. **AGENTS.md** - Project tech stack
5. **Documentation/Architecture.md** - Technology stack table

### Version History

| Date | .NET | C# | Aspire | Azure CLI | azd |
|------|------|-------|--------|-----------|-----|
| 2025-11-12 | **10.0.100 LTS** | **C# 14** | 13.0.0 | 2.79.0 | 1.21.1 |
| 2025-11-11 | 9.0.109 | C# 13 | 13.0.0 | 2.79.0 | 1.21.1 |

---

## 🔍 How to Check for Updates

### .NET SDK

```bash
# Current version
dotnet --version

# Check for updates
brew upgrade dotnet

# Or download from: https://dotnet.microsoft.com/download/dotnet/9.0
```

### Aspire

```bash
# Current workload
dotnet workload list

# Update workload
dotnet workload update

# Update templates
dotnet new install Aspire.ProjectTemplates --force

# Check package versions
cd Evermail
dotnet list package --outdated | grep Aspire
```

### Azure CLI

```bash
# Current version
az version

# Update
az upgrade
```

### Azure Developer CLI

```bash
# Current version
azd version

# Update (macOS)
brew upgrade azd

# Update (Windows)
choco upgrade azd-cli

# Update (Linux)
curl -fsSL https://aka.ms/install-azd.sh | bash
```

---

## 🚨 Breaking Change Alerts

### Sources to Monitor

- 📧 [.NET Blog](https://devblogs.microsoft.com/dotnet/)
- 📧 [Aspire GitHub Releases](https://github.com/dotnet/aspire/releases)
- 📧 [Azure Updates](https://azure.microsoft.com/updates/)

### When Breaking Changes Announced

1. ✅ Read migration guide
2. ✅ Create feature branch for update
3. ✅ Update one tool at a time
4. ✅ Run full test suite
5. ✅ Update documentation
6. ✅ Merge after verification

---

## 🎯 Current Status (2025-11-11)

**All tools**: ✅ Latest versions  
**All packages**: ✅ Latest stable  
**Build**: ✅ Success  
**Ready**: ✅ For development

**Next check**: 2025-12-01 (monthly)

---

**Last Updated**: 2025-11-11  
**Verified By**: Manual version checks  
**Status**: ✅ All up to date

