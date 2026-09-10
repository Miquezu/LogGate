namespace LogGate.Models;

public class DashboardMetrics
{
    public int TotalPasses { get; set; }
    public int UniqueEmployees { get; set; }
    public int LateCount { get; set; }
    public int EarlyCount { get; set; }
    public int AlcoPositiveCount { get; set; }
    public int HighTempCount { get; set; }
    public int MissingPassCount { get; set; }

    public int[] HourlyIn { get; set; } = new int[24];
    public int[] HourlyOut { get; set; } = new int[24];

    public List<ViolationCategoryCount> ViolationBreakdown { get; set; } = [];
    public List<DepartmentViolationCount> TopDepartments { get; set; } = [];
}

public record ViolationCategoryCount(string Category, int Count);
public record DepartmentViolationCount(string Department, int Count);

