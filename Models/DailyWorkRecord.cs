using System.Globalization;

namespace LogGate.Models;

/// <summary>
/// Суточная сводная запись рабочего дня сотрудника для табеля учёта рабочего времени.
/// </summary>
public class DailyWorkRecord
{
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    public DateTime Date { get; set; }

    public string DateFormatted => Date.ToString("dd.MM.yyyy");

    public string DayOfWeekFormatted
    {
        get
        {
            var dow = Date.ToString("ddd", RussianCulture);
            return $"{Date:dd.MM.yyyy} ({char.ToUpper(dow[0]) + dow[1..]})";
        }
    }

    public TimeSpan? FirstEntry { get; set; }
    public string FirstEntryFormatted => FirstEntry.ToShortTimeFormatted();

    public TimeSpan? LastExit { get; set; }
    public string LastExitFormatted => LastExit.ToShortTimeFormatted();

    public int PassesCount { get; set; }

    public TimeSpan WorkedTime { get; set; }
    public string WorkedTimeFormatted => WorkedTime.ToHoursMinutesString();

    public TimeSpan PlannedTime { get; set; }
    public string PlannedTimeFormatted => PlannedTime.ToPlanHoursMinutesString();

    public TimeSpan Balance { get; set; }
    public string BalanceFormatted => Balance.ToBalanceString(PlannedTime);

    public bool IsOvertime => PlannedTime > TimeSpan.Zero && Balance.TotalMinutes > 0;
    public bool IsUndertime => PlannedTime > TimeSpan.Zero && Balance.TotalMinutes < 0;
    public bool IsOnSchedule => PlannedTime > TimeSpan.Zero && (int)Balance.TotalMinutes == 0;
    public bool HasPlan => PlannedTime > TimeSpan.Zero;

    public bool IsLate { get; set; }
    public bool IsEarlyDeparture { get; set; }
    public bool HasAlcotestViolation { get; set; }
    public bool IsShortenedDay { get; set; }

    public string StatusText => this switch
    {
        { HasAlcotestViolation: true } => "Алкотест!",
        { IsLate: true, IsEarlyDeparture: true } => "Опоздание + Ранний уход",
        { IsLate: true } => "Опоздание",
        { IsEarlyDeparture: true } => "Ранний уход",
        { IsOvertime: true } => "Переработка",
        { IsUndertime: true } => "Недоработка",
        { HasPlan: true } => "Норма",
        _ => "Вне графика"
    };
}

