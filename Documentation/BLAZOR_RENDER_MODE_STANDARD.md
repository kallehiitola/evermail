# Blazor Render Mode Standard for Evermail

**Last Updated**: 2025-11-14  
**Based On**: [Official .NET 10 Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0)

---

## ✅ Evermail Standard: **InteractiveServer** for 90% of pages

### Quick Reference

```razor
@page "/your-page"
@rendermode InteractiveServer
@inject HttpClient Http
@inject NavigationManager Navigation

<h1>Your Interactive Page</h1>
<button @onclick="HandleClick">Click me</button>

@code {
    private void HandleClick() 
    {
        // Interactivity works! ✅
    }
}
```

---

## The 4 Render Modes (.NET 10)

| Mode | Interactive? | Location | Use When |
|------|-------------|----------|----------|
| **Static Server** (default) | ❌ No | Server | Landing page, About, Terms |
| **InteractiveServer** ⭐ | ✅ Yes | Server | **Login, Register, Email Viewer** |
| **InteractiveWebAssembly** | ✅ Yes | Client | Offline features (Phase 2+) |
| **InteractiveAuto** | ✅ Yes | Both | Best of both worlds (Phase 2+) |

⭐ **This is the Evermail standard**

---

## Decision Tree

```
Need interactivity? (@onclick, @bind, forms)
├─ NO  → Use default (Static Server)
│
└─ YES → Use InteractiveServer ⭐
         (Unless you need offline capability)
```

---

## Why InteractiveServer?

✅ **Perfect for Evermail because**:
- All code stays on server (secure)
- Full .NET API access (EF Core, Identity, ASP.NET Core)
- Access to HttpContext (cookies, auth)
- Real-time updates via SignalR
- Fast initial load (no WASM download)
- Perfect for SaaS with authentication

---

## Examples by Page Type

### ✅ Authentication (Login, Register)
```razor
@page "/login"
@rendermode InteractiveServer
```
**Why?** Needs forms, @onclick buttons, OAuth redirects

### ✅ Email Viewer/Search
```razor
@page "/emails"
@rendermode InteractiveServer
```
**Why?** Real-time search, database queries, navigation

### ✅ Admin Dashboard
```razor
@page "/admin"
@rendermode InteractiveServer

<AuthorizeView Roles="Admin,SuperAdmin">
    <Authorized>
        <!-- Admin UI -->
    </Authorized>
    <NotAuthorized>
        <CheckAuthAndRedirect />
    </NotAuthorized>
</AuthorizeView>
```
**Why?** Sensitive data, server-side operations

### ❌ Landing Page (Static)
```razor
@page "/"
@* No @rendermode - uses Static Server by default *@
```
**Why?** No interactivity needed, faster, SEO-friendly

---

## Common Mistakes to Avoid

### ❌ BAD: Using Static (default) for interactive pages

```razor
@page "/login"
@* Missing @rendermode InteractiveServer *@

<button @onclick="HandleLogin">Login</button>
@code {
    private void HandleLogin() 
    {
        // ❌ Won't work! Button does nothing!
    }
}
```

### ✅ GOOD: Add InteractiveServer

```razor
@page "/login"
@rendermode InteractiveServer

<button @onclick="HandleLogin">Login</button>
@code {
    private void HandleLogin() 
    {
        // ✅ Works! Button is interactive!
    }
}
```

---

## Migration Checklist

When creating a new page:

- [ ] Does it need `@onclick`, `@bind`, or forms?
  - **NO** → Leave default (Static Server)
  - **YES** → Add `@rendermode InteractiveServer`
- [ ] Add required `@inject` services (HttpClient, NavigationManager, etc.)
- [ ] Test that buttons/forms work
- [ ] Commit with clear message about render mode

---

## Reference Documentation

**Full documentation**: `.cursor/rules/blazor-frontend.mdc` (section: "Render Modes Standard")

**Official Microsoft Docs**: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0

---

## Summary

**Evermail Standard**: Use `@rendermode InteractiveServer` for all pages that need interactivity.

**When to deviate**: Only for offline features in Phase 2+ (then consider InteractiveAuto).

**Remember**: Static is default, so always add `@rendermode InteractiveServer` when you need interactivity!

---

## Authorization Pattern Reminder

- ❌ **Never** add `@attribute [Authorize]` to Blazor components. It prevents the router from rendering redirects/404 pages.
- ✅ Use `<AuthorizeRouteView>` (see `Components/Routes.razor`) for global handling.
- ✅ Each protected page should wrap content in `<AuthorizeView>` and render `<CheckAuthAndRedirect />` for the `<NotAuthorized>` block so anonymous users are redirected to `/login?returnUrl=/requested-path`.

