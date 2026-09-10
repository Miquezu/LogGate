using LogGate.Models;

namespace LogGate.Interfaces;

public interface IDashboardService
{
    DashboardMetrics CalculateMetrics(IEnumerable<DataItem> items);
}

