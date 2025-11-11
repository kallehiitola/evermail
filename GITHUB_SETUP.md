# GitHub Repository Setup Guide

This guide will help you push your Evermail project to GitHub.

## Quick Steps

### 1. Create GitHub Repository

Go to [GitHub](https://github.com/new) and create a new repository:

- **Repository name**: `evermail`
- **Description**: Modern email archive viewer and search platform powered by AI
- **Visibility**: Choose Public or Private
- **DO NOT** initialize with README, .gitignore, or license (we already have these)

### 2. Link Your Local Repository

Once you've created the GitHub repository, run these commands:

```bash
# Repository already set up at: https://github.com/kallehiitola/evermail
# Remote origin: git@github.com:kallehiitola/evermail.git

# To clone on another machine:
git clone git@github.com:kallehiitola/evermail.git

# To push future changes:
git push origin master

# If you want to rename master to main (GitHub's default):
git branch -M main
git push -u origin main
```

### 3. Set Up Branch Protection (Recommended)

On GitHub, go to your repository settings:

1. Navigate to **Settings → Branches**
2. Add branch protection rule for `main` (or `master`)
3. Enable:
   - ✅ Require pull request reviews before merging
   - ✅ Require status checks to pass before merging
   - ✅ Require branches to be up to date before merging

### 4. Add Repository Topics

On your GitHub repository page, click "Add topics" and add:
- `csharp`
- `dotnet`
- `azure`
- `aspire`
- `blazor`
- `email`
- `saas`
- `mbox`
- `email-archiving`
- `ai-search`

### 5. Configure GitHub Actions (Optional)

Create `.github/workflows/ci.yml` for continuous integration:

```yaml
name: CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore --configuration Release
    
    - name: Test
      run: dotnet test --no-build --configuration Release --verbosity normal
```

### 6. Enable GitHub Issues

Go to **Settings → Features** and ensure **Issues** is enabled for bug tracking and feature requests.

### 7. Add Repository Description

Update your repository description and website:
- **Description**: Modern email archive viewer and search platform powered by AI
- **Website**: `https://evermail.com` (or your actual domain)

### 8. Star Your Own Repository

Don't forget to star your own repository to show it's actively maintained! ⭐

## Collaboration Setup

### Add Collaborators

If working with a team:
1. Go to **Settings → Collaborators**
2. Add team members with appropriate access levels

### Set Up Projects

Use GitHub Projects for task management:
1. Go to **Projects → New project**
2. Create boards for:
   - MVP Development
   - Phase 2 Features
   - Bug Tracking

## Next Steps

After pushing to GitHub:

1. **Set up Azure resources** (see `Documentation/Deployment.md`)
2. **Configure Stripe** for payment processing
3. **Start building** the Aspire solution structure
4. **Deploy MVP** to Azure Container Apps

---

**Need Help?**

If you encounter issues:
- Check [GitHub's documentation](https://docs.github.com/en/get-started/importing-your-projects-to-github/importing-source-code-to-github/adding-locally-hosted-code-to-github)
- Ensure you have SSH keys set up or use HTTPS with personal access token
- Run `git remote -v` to verify your remote is configured correctly

Good luck with your Evermail project! 🚀

