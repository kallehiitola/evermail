# Contributing to Evermail

Thank you for your interest in contributing to Evermail! This document provides guidelines and instructions for contributing to the project.

## Code of Conduct

Be respectful, constructive, and professional in all interactions. We're building a positive community around email archiving and modern .NET development.

## Development Setup

See [README.md](README.md#getting-started) for local development setup instructions.

## How to Contribute

### Reporting Bugs

1. Check if the bug has already been reported in [GitHub Issues](https://github.com/yourusername/evermail/issues)
2. If not, create a new issue with:
   - Clear title and description
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version, browser)
   - Screenshots if applicable

### Suggesting Features

1. Open a [GitHub Issue](https://github.com/yourusername/evermail/issues/new) with the `enhancement` label
2. Describe the feature and its use case
3. Explain why it would be valuable to users
4. Consider implementation complexity and alternatives

### Pull Requests

1. **Fork the repository** and create a feature branch from `develop`
   ```bash
   git checkout develop
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the coding standards (see below)

3. **Write tests** for new functionality
   - Unit tests for business logic
   - Integration tests for API endpoints
   - E2E tests for critical user flows

4. **Update documentation**
   - Update relevant files in `/Documentation` folder
   - Add XML comments to public APIs
   - Update README.md if needed

5. **Run tests and linting**
   ```bash
   dotnet test
   dotnet format
   ```

6. **Commit your changes** with a clear commit message
   ```bash
   git commit -m "feat: add email export to PDF feature"
   ```

7. **Push to your fork** and submit a pull request
   ```bash
   git push origin feature/your-feature-name
   ```

## Coding Standards

### Follow .cursorrules

The project has a comprehensive `.cursorrules` file that defines coding conventions. Key points:

- Use C# 12+ features (file-scoped namespaces, global usings, records)
- Follow Microsoft naming conventions
- Use async/await consistently
- Prefer dependency injection over static classes
- Always filter by `TenantId` for multi-tenant queries

### Code Style

```csharp
// Good ✅
public async Task<EmailMessage?> GetEmailAsync(Guid id, CancellationToken ct)
{
    return await _context.EmailMessages
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Id == id, ct);
}

// Bad ❌
public EmailMessage GetEmail(Guid id)
{
    return _context.EmailMessages.Find(id);
}
```

### Security

- **Never** bypass tenant isolation filters
- **Always** validate user input
- **Never** log sensitive data (passwords, tokens, email content)
- Use parameterized queries (EF Core does this automatically)
- Sanitize HTML before rendering

### Testing

```csharp
public class EmailServiceTests
{
    [Fact]
    public async Task GetEmailAsync_WithValidId_ReturnsEmail()
    {
        // Arrange
        var email = new EmailMessage { /* ... */ };
        var mockRepo = new Mock<IEmailRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(email);
        var service = new EmailService(mockRepo.Object);
        
        // Act
        var result = await service.GetEmailAsync(email.Id);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(email.Id, result.Id);
    }
}
```

## Commit Message Guidelines

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
type(scope): subject

body (optional)

footer (optional)
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, no logic change)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Build process, tooling changes

**Examples**:
```
feat(search): add semantic search with Azure AI
fix(auth): resolve 2FA token validation issue
docs(api): update endpoint documentation
refactor(parser): improve mbox parsing performance
test(integration): add tests for mailbox upload
chore(deps): upgrade to .NET 9
```

## Branch Strategy

- `main` - Production-ready code
- `develop` - Integration branch for features
- `feature/*` - Feature branches (merge into develop)
- `hotfix/*` - Urgent production fixes (merge into main and develop)

## Pull Request Checklist

Before submitting your PR, ensure:

- [ ] Code follows `.cursorrules` conventions
- [ ] All tests pass (`dotnet test`)
- [ ] Code is formatted (`dotnet format`)
- [ ] Documentation is updated
- [ ] Commit messages follow conventions
- [ ] No sensitive data in code or comments
- [ ] Multi-tenant isolation is maintained
- [ ] New dependencies are justified and documented

## Review Process

1. **Automated Checks**: CI pipeline runs tests, linting, security scans
2. **Code Review**: At least one maintainer reviews the PR
3. **Feedback**: Address any comments or requested changes
4. **Approval**: Once approved, maintainer will merge

## Getting Help

- **Questions**: Open a [GitHub Discussion](https://github.com/yourusername/evermail/discussions)
- **Real-time Chat**: Join our [Discord](https://discord.gg/evermail)
- **Email**: dev@evermail.com

## Recognition

Contributors will be:
- Listed in CONTRIBUTORS.md
- Mentioned in release notes
- Invited to exclusive contributor events

## License

By contributing to Evermail, you agree that your contributions will be licensed under the MIT License.

---

Thank you for making Evermail better! 🚀

