using System.Globalization;
using System.Text.RegularExpressions;

namespace WinformsUI;

/// <summary>Small deterministic parser for explicit dates and common Japanese/English relative dates.</summary>
public static partial class NaturalDates
{
    public static bool TryParse(string text, out DateTime date, DateTime? today = null, CultureInfo? culture = null)
    {
        date = default;
        string input = text.Trim().ToLowerInvariant(); DateTime anchor = (today ?? DateTime.Today).Date;
        if (input is "今日" or "本日" or "today") { date = anchor; return true; }
        if (input is "明日" or "tomorrow") { date = anchor.AddDays(1); return true; }
        if (input is "昨日" or "yesterday") { date = anchor.AddDays(-1); return true; }
        if (input is "来週" or "next week") { date = anchor.AddDays(7); return true; }
        var relative = RelativeDays().Match(input);
        if (relative.Success && int.TryParse(relative.Groups[1].Value, out int days) && days < 100000)
        {
            bool previous = relative.Groups[2].Value is "前" or "ago";
            try { date = anchor.AddDays(previous ? -days : days); return true; } catch (ArgumentOutOfRangeException) { return false; }
        }
        string[] japanese = ["日", "月", "火", "水", "木", "金", "土"];
        string[] english = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];
        for (int day = 0; day < 7; day++)
        {
            if (input == $"来週{japanese[day]}曜" || input == $"来週{japanese[day]}曜日")
            {
                int monday = (8 - (int)anchor.DayOfWeek) % 7; if (monday == 0) monday = 7;
                date = anchor.AddDays(monday + (day + 6) % 7); return true;
            }
            if (input == $"next {english[day]}" || input == $"次の{japanese[day]}曜" || input == $"次の{japanese[day]}曜日")
            {
                int delta = (day - (int)anchor.DayOfWeek + 7) % 7;
                date = anchor.AddDays(delta == 0 ? 7 : delta); return true;
            }
        }
        if (!DateTime.TryParse(text, culture ?? CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var explicitDate)) return false;
        date = explicitDate.Date; return true;
    }
    [GeneratedRegex(@"^(\d+)\s*(?:日|days?)\s*(後|前|later|ago)$", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeDays();
}
