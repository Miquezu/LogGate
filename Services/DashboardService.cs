using LogGate.Interfaces;
using LogGate.Models;

namespace LogGate.Services;

public class DashboardService : IDashboardService
{
    public DashboardMetrics CalculateMetrics(IEnumerable<DataItem> items)
    {
        var metrics = new DashboardMetrics();
        var uniqueEmployees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deptViolations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            metrics.TotalPasses++;

            if (!string.IsNullOrWhiteSpace(item.FullName))
            {
                uniqueEmployees.Add(!string.IsNullOrWhiteSpace(item.EmployeeNumber) ? item.EmployeeNumber : item.FullName);
            }

            if (item.EventTime is { } time)
            {
                int hour = time.Hour;
                if (hour is >= 0 and < 24)
                {
                    if (string.Equals(item.Direction, "Вход", StringComparison.OrdinalIgnoreCase))
                    {
                        metrics.HourlyIn[hour]++;
                    }
                    else if (string.Equals(item.Direction, "Выход", StringComparison.OrdinalIgnoreCase))
                    {
                        metrics.HourlyOut[hour]++;
                    }
                }
            }

            bool hasViolation = false;

            if (item.IsLate)
            {
                metrics.LateCount++;
                hasViolation = true;
            }

            if (item.IsEarlyDeparture)
            {
                metrics.EarlyCount++;
                hasViolation = true;
            }

            if (item.AlcotestResult > 0)
            {
                metrics.AlcoPositiveCount++;
                hasViolation = true;
            }

            if (item.Temperature > 37.2)
            {
                metrics.HighTempCount++;
                hasViolation = true;
            }

            if (!string.IsNullOrWhiteSpace(item.SystemNote) &&
                item.SystemNote.Contains("Пропущен", StringComparison.OrdinalIgnoreCase))
            {
                metrics.MissingPassCount++;
                hasViolation = true;
            }

            if (hasViolation)
            {
                string dept = !string.IsNullOrWhiteSpace(item.Department) ? item.Department.Trim() : "Без отдела";
                deptViolations[dept] = deptViolations.GetValueOrDefault(dept) + 1;
            }
        }

        metrics.UniqueEmployees = uniqueEmployees.Count;

        // Структура категорий нарушений
        metrics.ViolationBreakdown =
        [
            new("Опоздания", metrics.LateCount),
            new("Ранние уходы", metrics.EarlyCount),
            new("Пропущен проход", metrics.MissingPassCount),
            new("Температура > 37.2°C", metrics.HighTempCount),
            new("Положительный алкотест", metrics.AlcoPositiveCount)
        ];

        // Топ-5 отделов с наибольшим числом нарушений
        metrics.TopDepartments = deptViolations
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => new DepartmentViolationCount(kv.Key, kv.Value))
            .ToList();

        return metrics;
    }
}

