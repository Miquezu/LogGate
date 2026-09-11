namespace LogGate.Models;

/// <summary>
/// Результат комплексного расчёта табеля учёта рабочего времени сотрудника.
/// </summary>
public class TimesheetResult
{
    public List<DailyWorkRecord> DailyRecords { get; set; } = [];
    public TimesheetSummary Summary { get; set; } = new();
}

