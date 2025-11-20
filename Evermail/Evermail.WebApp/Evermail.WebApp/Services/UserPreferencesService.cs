using System.Net.Http.Headers;
using System.Net.Http.Json;
using Evermail.Common.DTOs;
using Evermail.Common.DTOs.User;
using Evermail.WebApp.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Evermail.WebApp.Services;

public class UserPreferencesService
{
    private const string JsNamespace = "EvermailPreferences";
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationStateService _authStateService;
    private readonly NavigationManager _navigationManager;
    private UserPreferences _cache = UserPreferences.CreateDefault();
    private bool _hasLoaded;

    public UserPreferencesService(
        IJSRuntime jsRuntime,
        HttpClient httpClient,
        IAuthenticationStateService authStateService,
        NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _httpClient = httpClient;
        _authStateService = authStateService;
        _navigationManager = navigationManager;
    }

    public async Task<UserPreferences> GetPreferencesAsync()
    {
        if (_hasLoaded)
        {
            return _cache;
        }

        var serverPreferences = await TryFetchFromServerAsync();
        if (serverPreferences is not null)
        {
            _cache = serverPreferences;
            await PersistToLocalStorageAsync(_cache);
            _hasLoaded = true;
            return _cache;
        }

        var local = await TryReadFromLocalStorageAsync();
        _cache = local ?? UserPreferences.CreateDefault();
        _hasLoaded = true;
        return _cache;
    }

    public async Task<UserPreferences> SaveAsync(UserPreferences preferences)
    {
        _cache = preferences;
        _hasLoaded = true;

        await TryPersistToServerAsync(preferences);
        await PersistToLocalStorageAsync(preferences);

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

    public async Task<UserPreferences> SetResultDensityAsync(ResultDensityPreference preference)
    {
        var prefs = await GetPreferencesAsync();
        prefs.ResultDensity = preference;
        return await SaveAsync(prefs);
    }

    public async Task<UserPreferences> SetKeyboardShortcutsAsync(bool enabled)
    {
        var prefs = await GetPreferencesAsync();
        prefs.KeyboardShortcutsEnabled = enabled;
        return await SaveAsync(prefs);
    }

    public async Task<UserPreferences> SetMatchNavigatorAsync(bool enabled)
    {
        var prefs = await GetPreferencesAsync();
        prefs.MatchNavigatorEnabled = enabled;
        return await SaveAsync(prefs);
    }

    private async Task<UserPreferences?> TryFetchFromServerAsync()
    {
        try
        {
            var token = await EnsureTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_navigationManager.BaseUri}api/v1/users/me/settings/display");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<ApiResponse<UserDisplaySettingsDto>>();
            if (payload?.Success != true || payload.Data is null)
            {
                return null;
            }

            return MapFromDto(payload.Data);
        }
        catch
        {
            return null;
        }
    }

    private async Task TryPersistToServerAsync(UserPreferences preferences)
    {
        try
        {
            var token = await EnsureTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"{_navigationManager.BaseUri}api/v1/users/me/settings/display")
            {
                Content = JsonContent.Create(BuildUpdateRequest(preferences))
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            await _httpClient.SendAsync(request);
        }
        catch
        {
            // Ignore network errors; local cache still updated
        }
    }

    private async Task PersistToLocalStorageAsync(UserPreferences preferences)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync($"{JsNamespace}.set", preferences);
        }
        catch (InvalidOperationException)
        {
            // JS runtime not available (SSR)
        }
    }

    private async Task<UserPreferences?> TryReadFromLocalStorageAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<UserPreferences?>($"{JsNamespace}.get");
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<string?> EnsureTokenAsync()
    {
        var token = await _authStateService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            return token;
        }

        await _authStateService.RefreshTokenIfNeededAsync();
        return await _authStateService.GetTokenAsync();
    }

    private static UserPreferences MapFromDto(UserDisplaySettingsDto dto) =>
        new()
        {
            DateFormat = UserPreferences.FromServerFormat(dto.DateFormat),
            ResultDensity = UserPreferences.FromServerDensity(dto.ResultDensity),
            AutoScrollToKeyword = dto.AutoScrollToKeyword,
            MatchNavigatorEnabled = dto.MatchNavigatorEnabled,
            KeyboardShortcutsEnabled = dto.KeyboardShortcutsEnabled
        };

    private static UpdateUserDisplaySettingsRequest BuildUpdateRequest(UserPreferences preferences) =>
        new(
            DateFormat: UserPreferences.ToServerFormat(preferences.DateFormat),
            ResultDensity: UserPreferences.ToServerDensity(preferences.ResultDensity),
            AutoScrollToKeyword: preferences.AutoScrollToKeyword,
            MatchNavigatorEnabled: preferences.MatchNavigatorEnabled,
            KeyboardShortcutsEnabled: preferences.KeyboardShortcutsEnabled);
}

