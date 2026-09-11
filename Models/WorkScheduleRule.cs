namespace LogGate.Models;

/// <summary>
/// Правило рабочего графика для подразделения или конкретного сотрудника.
/// </summary>
public class WorkScheduleRule
{
    public int Id { get; set; }
    public string? TargetName { get; set; }
    public bool IsPersonal { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool RequiresAlcotest { get; set; }
}
