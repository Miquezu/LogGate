namespace LogGate.Models;

/// <summary>
/// Сводные агрегированные показатели рабочего времени сотрудника за отчётный период.
/// </summary>
public class TimesheetSummary
{
    public int TotalWorkDays { get; set; }

    public TimeSpan TotalWorkedTime { get; set; }
    public string TotalWorkedTimeFormatted => TotalWorkedTime.ToHoursMinutesString();

    public TimeSpan TotalPlannedTime { get; set; }
    public string TotalPlannedTimeFormatted => TotalPlannedTime.ToPlanHoursMinutesString();

    public TimeSpan TotalBalance { get; set; }
    public string TotalBalanceFormatted => TotalBalance.ToBalanceString(TotalPlannedTime);

    public bool IsTotalOvertime => TotalPlannedTime > TimeSpan.Zero && TotalBalance.TotalMinutes > 0;
    public bool IsTotalUndertime => TotalPlannedTime > TimeSpan.Zero && TotalBalance.TotalMinutes < 0;

    public double AverageHoursPerDay => TotalWorkDays > 0 ? TotalWorkedTime.TotalHours / TotalWorkDays : 0.0;
    public string AverageHoursPerDayFormatted => AverageHoursPerDay.ToAverageHoursMinutesString();

    public int TotalLateCount { get; set; }
    public int TotalEarlyCount { get; set; }
    public int TotalAlcotestViolations { get; set; }
}

