using LogGate.Interfaces;
using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LogGate.Services
{
    public class ScheduleService : IScheduleService
    {
        public List<DateTime> PreHolidays { get; private set; } = new();
        public List<WorkScheduleRule> Rules { get; private set; } = new();

        public void EvaluateCompliance(IEnumerable<DataItem> items)
        {
            if (Rules.Count == 0) return;

            var validGroups = items
                .Where(x => !string.IsNullOrWhiteSpace(x.FullName) && x.EventTime.HasValue)
                .GroupBy(x => new { x.FullName, Date = x.EventTime!.Value.Date });

            foreach (var group in validGroups)
            {
                var dayEvents = group.OrderBy(x => x.EventTime).ToList();
                var firstItem = dayEvents.First();

                // Исключение службы охраны и технических пропусков из дневного нормоконтроля
                bool isSecurity = (firstItem.Department != null && firstItem.Department.Contains("охраны", StringComparison.OrdinalIgnoreCase)) ||
                                  (firstItem.Position != null && firstItem.Position.Contains("контролер на", StringComparison.OrdinalIgnoreCase));

                bool isTechnicalCard = firstItem.FullName!.StartsWith('{') && firstItem.FullName.EndsWith('}');

                if (isSecurity || isTechnicalCard)
                    continue;

                // Поиск регламента: если правило не найдено, нарушения НЕ фиксируются во избежание ложных выводов
                var rule = GetRuleFor(firstItem);
                if (rule == null)
                    continue;

                // 1. Опоздание: оценивается только первый вход за сутки
                var firstEntry = dayEvents.FirstOrDefault(x => string.Equals(x.Direction, "Вход", StringComparison.OrdinalIgnoreCase));
                if (firstEntry != null)
                {
                    int startMinutes = (int)rule.StartTime.TotalMinutes;
                    int eventMinutes = firstEntry.EventTime!.Value.Hour * 60 + firstEntry.EventTime.Value.Minute;

                    if (eventMinutes > startMinutes + 1)
                    {
                        firstEntry.IsLate = true;
                    }
                }

                // 2. Ранний уход: оценивается только финальный выход смены
                var lastExit = dayEvents.LastOrDefault(x => string.Equals(x.Direction, "Выход", StringComparison.OrdinalIgnoreCase));
                if (lastExit != null)
                {
                    int normalEnd = (int)rule.EndTime.TotalMinutes;
                    int shortEnd = normalEnd - 60;
                    int eventMinutes = lastExit.EventTime!.Value.Hour * 60 + lastExit.EventTime.Value.Minute;

                    bool isShortDay = PreHolidays.Contains(lastExit.EventTime!.Value.Date);
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

            // Персональное сопоставление
            var personalRule = Rules.FirstOrDefault(r => r.IsPersonal &&
                string.Equals(r.TargetName?.Trim(), item.FullName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (personalRule != null) return personalRule;

            // Сопоставление по подразделению
            if (!string.IsNullOrWhiteSpace(item.Department))
            {
                return Rules.FirstOrDefault(r => !r.IsPersonal &&
                    string.Equals(r.TargetName?.Trim(), item.Department.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        public bool IsEarlyDeparture(DataItem x) => x.IsEarlyDeparture;

        public bool IsLate(DataItem x) => x.IsLate;

        public bool RequiresAlcotest(DataItem item)
        {
            var rule = GetRuleFor(item);
            return rule?.RequiresAlcotest ?? false;
        }

        public void UpdateRules(List<WorkScheduleRule> rules, List<DateTime> holidays)
        {
            Rules = rules;
            PreHolidays = holidays;
        }
    }
}