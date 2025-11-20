namespace Evermail.WebApp.Models;

public enum DateFormatPreference
{
    MonthDayYear,
    DayMonthYear
}

public class UserPreferences
{
    public DateFormatPreference DateFormat { get; set; } = DateFormatPreference.MonthDayYear;
    public bool AutoScrollToKeyword { get; set; } = true;

    public static UserPreferences CreateDefault() => new();
}

