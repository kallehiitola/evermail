using Evermail.WebApp.Models;
using Microsoft.JSInterop;

namespace Evermail.WebApp.Services;

public class UserPreferencesService
{
    private const string JsNamespace = "EvermailPreferences";
    private readonly IJSRuntime _jsRuntime;
    private UserPreferences _cache = UserPreferences.CreateDefault();
    private bool _hasLoaded;

    public UserPreferencesService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<UserPreferences> GetPreferencesAsync()
    {
        if (_hasLoaded)
        {
            return _cache;
        }

        try
        {
            var prefs = await _jsRuntime.InvokeAsync<UserPreferences?>($"{JsNamespace}.get");
            _cache = prefs ?? UserPreferences.CreateDefault();
            _hasLoaded = true;
        }
        catch (InvalidOperationException)
        {
            // JS runtime not available during prerendering; keep defaults
        }

        return _cache;
    }

    public async Task<UserPreferences> SaveAsync(UserPreferences preferences)
    {
        _cache = preferences;
        _hasLoaded = true;

        try
        {
            await _jsRuntime.InvokeVoidAsync($"{JsNamespace}.set", preferences);
        }
        catch (InvalidOperationException)
        {
            // JS runtime not available yet
        }

        return _cache;
    }

    public async Task<UserPreferences> SetDateFormatAsync(DateFormatPreference preference)
    {
        var prefs = await GetPreferencesAsync();
        prefs.DateFormat = preference;
        return await SaveAsync(prefs);
    }

    public async Task<UserPreferences> SetAutoScrollAsync(bool enabled)
    {
        var prefs = await GetPreferencesAsync();
        prefs.AutoScrollToKeyword = enabled;
        return await SaveAsync(prefs);
    }
}

