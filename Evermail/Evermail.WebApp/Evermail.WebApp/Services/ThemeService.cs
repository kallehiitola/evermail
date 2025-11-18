using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Evermail.WebApp.Services;

public class ThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private const string JsNamespace = "EvermailTheme";

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ValueTask<string?> GetThemeAsync()
        => _jsRuntime.InvokeAsync<string?>(($"{JsNamespace}.getTheme"));

    public ValueTask<string?> SetThemeAsync(string theme)
        => _jsRuntime.InvokeAsync<string?>($"{JsNamespace}.setTheme", theme);

    public ValueTask<string?> ToggleThemeAsync()
        => _jsRuntime.InvokeAsync<string?>($"{JsNamespace}.toggleTheme");
}

