namespace LogGate.Extensions;

/// <summary>
/// Методы расширения для форматирования интервалов времени и баланса рабочего времени.
/// </summary>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Форматирует интервал времени в строку вида "X ч YY мин".
    /// </summary>
    public static string ToHoursMinutesString(this TimeSpan span)
    {
        int hours = (int)span.TotalHours;
        int minutes = Math.Abs(span.Minutes);
        return $"{hours} ч {minutes:D2} мин";
    }

    /// <summary>
    /// Форматирует нормативное запланированное время. Возвращает "—", если норма равна нулю или отрицательна.
    /// </summary>
    public static string ToPlanHoursMinutesString(this TimeSpan span) =>
        span <= TimeSpan.Zero ? "—" : span.ToHoursMinutesString();

    /// <summary>
    /// Форматирует баланс рабочего времени (+/-) относительно запланированной нормы.
    /// </summary>
    public static string ToBalanceString(this TimeSpan balance, TimeSpan plannedTime)
    {
        if (plannedTime <= TimeSpan.Zero)
            return "—";

        int totalMinutes = (int)balance.TotalMinutes;
        string sign = totalMinutes >= 0 ? "+" : "-";
        int absMinutes = Math.Abs(totalMinutes);
        int hours = absMinutes / 60;
        int minutes = absMinutes % 60;

        return hours > 0
            ? $"{sign}{hours} ч {minutes:D2} мин"
            : $"{sign}{minutes} мин";
    }

    /// <summary>
    /// Форматирует среднее количество часов (double) в строку вида "X ч YY мин".
    /// </summary>
    public static string ToAverageHoursMinutesString(this double averageHours)
    {
        int hours = (int)averageHours;
        int minutes = (int)Math.Round((averageHours - hours) * 60);
        return $"{hours} ч {minutes:D2} мин";
    }

    /// <summary>
    /// Форматирует nullable TimeSpan в строку "hh:mm" или "—".
    /// </summary>
    public static string ToShortTimeFormatted(this TimeSpan? time) =>
        time.HasValue ? time.Value.ToString(@"hh\:mm") : "—";
}

