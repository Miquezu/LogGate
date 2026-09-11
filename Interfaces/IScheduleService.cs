namespace LogGate.Interfaces;

public interface IScheduleService
{
    List<DateTime> PreHolidays { get; }
    List<WorkScheduleRule> Rules { get; }

    void EvaluateCompliance(IEnumerable<DataItem> items);

    WorkScheduleRule? GetRuleFor(DataItem item);

    bool IsEarlyDeparture(DataItem x);

    bool IsLate(DataItem x);

    bool RequiresAlcotest(DataItem item);

    void UpdateRules(List<WorkScheduleRule> rules, List<DateTime> holidays);
}
}