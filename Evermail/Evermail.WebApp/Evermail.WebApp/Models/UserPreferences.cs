namespace Evermail.WebApp.Models;

public enum DateFormatPreference
{
    MonthDayYear,
    DayMonthYear
}

public enum ResultDensityPreference
{
    Cozy,
    Compact
}

public class UserPreferences
{
    public DateFormatPreference DateFormat { get; set; } = DateFormatPreference.MonthDayYear;
    public bool AutoScrollToKeyword { get; set; } = true;
    public ResultDensityPreference ResultDensity { get; set; } = ResultDensityPreference.Cozy;
    public bool MatchNavigatorEnabled { get; set; } = true;
    public bool KeyboardShortcutsEnabled { get; set; } = true;

    public static UserPreferences CreateDefault() => new();

    public static DateFormatPreference FromServerFormat(string? format) =>
        format switch
        {
            "dd.MM.yyyy" => DateFormatPreference.DayMonthYear,
            _ => DateFormatPreference.MonthDayYear
        };

    public static string ToServerFormat(DateFormatPreference preference) =>
        preference switch
        {
            DateFormatPreference.DayMonthYear => "dd.MM.yyyy",
            _ => "MMM dd, yyyy"
        };

    public static ResultDensityPreference FromServerDensity(string? density) =>
        density?.Equals("Compact", StringComparison.OrdinalIgnoreCase) == true
            ? ResultDensityPreference.Compact
            : ResultDensityPreference.Cozy;

    public static string ToServerDensity(ResultDensityPreference preference) =>
        preference == ResultDensityPreference.Compact ? "Compact" : "Cozy";
}

