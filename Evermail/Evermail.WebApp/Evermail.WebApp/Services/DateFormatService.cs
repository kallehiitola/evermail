using System.Globalization;
using Evermail.WebApp.Models;

namespace Evermail.WebApp.Services;

public interface IDateFormatService
{
    string Format(DateTime date, DateFormatPreference preference);
}

public sealed class DateFormatService : IDateFormatService
{
    public string Format(DateTime date, DateFormatPreference preference)
    {
        var format = UserPreferences.ToServerFormat(preference);
        return date.ToString(format, CultureInfo.InvariantCulture);
    }
}

