using LogGate.Interfaces;
using LogGate.Models;

namespace LogGate.Services;

public class ScheduleService : IScheduleService
{
    private readonly Dictionary<string, WorkScheduleRule> _personalRules = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorkScheduleRule> _departmentRules = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<DateTime> _preHolidaysSet = [];

    public List<DateTime> PreHolidays { get; private set; } = [];
    public List<WorkScheduleRule> Rules { get; private set; } = [];

    public void EvaluateCompliance(IEnumerable<DataItem> items)
    {
        if (Rules.Count == 0) return;

        var validGroups = items
            .Where(x => !string.IsNullOrWhiteSpace(x.FullName) && x.EventTime.HasValue)
            .GroupBy(x => new { x.FullName, Date = x.EventTime!.Value.Date });

        foreach (var group in validGroups)
        {
            var dayEvents = group.OrderBy(x => x.EventTime).ToList();
            var firstItem = dayEvents[0];

            // Исключение службы охраны и технических пропусков из дневного нормоконтроля
            bool isSecurity = firstItem.Department?.Contains("охраны", StringComparison.OrdinalIgnoreCase) is true ||
                              firstItem.Position?.Contains("контролер на", StringComparison.OrdinalIgnoreCase) is true;

            bool isTechnicalCard = firstItem.FullName is ['{', .., '}'];

            if (isSecurity || isTechnicalCard)
                continue;

            // Поиск регламента O(1): если правило не найдено, нарушения НЕ фиксируются
            var rule = GetRuleFor(firstItem);
            if (rule is null)
                continue;

            // 1. Опоздание: оценивается только первый вход за сутки
            var firstEntry = dayEvents.FirstOrDefault(x => string.Equals(x.Direction, "Вход", StringComparison.OrdinalIgnoreCase));
            if (firstEntry?.EventTime is { } entryTime)
            {
                int startMinutes = (int)rule.StartTime.TotalMinutes;
                int eventMinutes = entryTime.Hour * 60 + entryTime.Minute;

                if (eventMinutes > startMinutes + 1)
                {
                    firstEntry.IsLate = true;
                }
            }

            // 2. Ранний уход: оценивается только финальный выход смены
            var lastExit = dayEvents.LastOrDefault(x => string.Equals(x.Direction, "Выход", StringComparison.OrdinalIgnoreCase));
            if (lastExit?.EventTime is { } exitTime)
            {
                int normalEnd = (int)rule.EndTime.TotalMinutes;
                int shortEnd = normalEnd - 60;
                int eventMinutes = exitTime.Hour * 60 + exitTime.Minute;

                bool isShortDay = _preHolidaysSet.Contains(exitTime.Date);
                int limit = isShortDay ? shortEnd : normalEnd;

                if (eventMinutes < limit)
                {
                    lastExit.IsEarlyDeparture = true;
                }
            }
        }
    }

    public WorkScheduleRule? GetRuleFor(DataItem item)
    {
        if (string.IsNullOrWhiteSpace(item.FullName)) return null;

        // Персональное сопоставление O(1)
        if (_personalRules.TryGetValue(item.FullName.Trim(), out var personalRule))
            return personalRule;

        // Сопоставление по подразделению O(1)
        if (!string.IsNullOrWhiteSpace(item.Department) &&
            _departmentRules.TryGetValue(item.Department.Trim(), out var depRule))
        {
            return depRule;
        }

        return null;
    }

    public bool IsEarlyDeparture(DataItem x) => x.IsEarlyDeparture;

    public bool IsLate(DataItem x) => x.IsLate;

    public bool RequiresAlcotest(DataItem item) => GetRuleFor(item)?.RequiresAlcotest ?? false;

    public void UpdateRules(List<WorkScheduleRule> rules, List<DateTime> holidays)
    {
        Rules = rules;
        PreHolidays = holidays;

        _personalRules.Clear();
        _departmentRules.Clear();
        _preHolidaysSet.Clear();

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.TargetName)) continue;
            var key = rule.TargetName.Trim();

            if (rule.IsPersonal)
                _personalRules[key] = rule;
            else
                _departmentRules[key] = rule;
        }

        foreach (var holiday in holidays)
        {
            _preHolidaysSet.Add(holiday.Date);
        }
    }
}