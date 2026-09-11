namespace LogGate.Services;

/// <summary>
/// Сервис расчёта фактического рабочего времени, соблюдения норм и баланса рабочего времени.
/// </summary>
public class TimesheetService : ITimesheetService
{
    public TimesheetResult CalculateTimesheet(
        IEnumerable<DataItem> employeeEvents,
        WorkScheduleRule? rule,
        IEnumerable<DateTime>? preHolidays = null)
    {
        var result = new TimesheetResult();
        HashSet<DateTime> preHolidaysSet = preHolidays != null
            ? [.. preHolidays.Select(d => d.Date)]
            : [];

        var validEvents = employeeEvents
            .Where(x => x.EventTime.HasValue)
            .OrderBy(x => x.EventTime)
            .ToList();

        if (validEvents.Count == 0)
            return result;

        var dayGroups = validEvents
            .GroupBy(x => x.EventTime!.Value.Date)
            .OrderByDescending(g => g.Key);

        TimeSpan totalWorked = TimeSpan.Zero;
        TimeSpan totalPlanned = TimeSpan.Zero;
        int totalLate = 0;
        int totalEarly = 0;
        int totalAlco = 0;

        foreach (var group in dayGroups)
        {
            var date = group.Key;
            var dayEvents = group.OrderBy(x => x.EventTime).ToList();

            var firstEntry = dayEvents.FirstOrDefault(x => string.Equals(x.Direction?.Trim(), "Вход", StringComparison.OrdinalIgnoreCase));
            var lastExit = dayEvents.LastOrDefault(x => string.Equals(x.Direction?.Trim(), "Выход", StringComparison.OrdinalIgnoreCase));

            TimeSpan worked = TimeSpan.Zero;
            DateTime? openEntry = null;

            foreach (var evt in dayEvents)
            {
                bool isEntry = string.Equals(evt.Direction?.Trim(), "Вход", StringComparison.OrdinalIgnoreCase);
                bool isExit = string.Equals(evt.Direction?.Trim(), "Выход", StringComparison.OrdinalIgnoreCase);

                if (isEntry)
                {
                    openEntry ??= evt.EventTime;
                }
                else if (isExit)
                {
                    if (openEntry.HasValue && evt.EventTime.HasValue && evt.EventTime.Value >= openEntry.Value)
                    {
                        worked += (evt.EventTime.Value - openEntry.Value);
                        openEntry = null;
                    }
                }
            }

            // Резервный расчет по крайним точкам, если события не были строго парными
            if (worked == TimeSpan.Zero && firstEntry?.EventTime != null && lastExit?.EventTime != null && lastExit.EventTime > firstEntry.EventTime)
            {
                worked = lastExit.EventTime.Value - firstEntry.EventTime.Value;
            }

            bool isShortDay = preHolidaysSet.Contains(date);
            TimeSpan planned = TimeSpan.Zero;

            if (rule != null)
            {
                TimeSpan span = rule.EndTime > rule.StartTime
                    ? rule.EndTime - rule.StartTime
                    : TimeSpan.FromHours(8);

                // Если продолжительность по графику 8.5+ часов (08:00 - 17:00), стандартная норма 8 ч
                planned = span >= TimeSpan.FromHours(8.5)
                    ? TimeSpan.FromHours(8)
                    : span;

                if (isShortDay && planned > TimeSpan.FromHours(1))
                {
                    planned = planned.Subtract(TimeSpan.FromHours(1));
                }
            }

            TimeSpan balance = planned > TimeSpan.Zero ? worked - planned : TimeSpan.Zero;

            bool isLate = dayEvents.Any(x => x.IsLate);
            bool isEarly = dayEvents.Any(x => x.IsEarlyDeparture);
            bool hasAlco = dayEvents.Any(x => x.HasAlcotestViolation);

            var record = new DailyWorkRecord
            {
                Date = date,
                FirstEntry = firstEntry?.EventTime?.TimeOfDay,
                LastExit = lastExit?.EventTime?.TimeOfDay,
                PassesCount = dayEvents.Count,
                WorkedTime = worked,
                PlannedTime = planned,
                Balance = balance,
                IsLate = isLate,
                IsEarlyDeparture = isEarly,
                HasAlcotestViolation = hasAlco,
                IsShortenedDay = isShortDay
            };

            result.DailyRecords.Add(record);

            totalWorked += worked;
            totalPlanned += planned;
            if (isLate) totalLate++;
            if (isEarly) totalEarly++;
            if (hasAlco) totalAlco++;
        }

        result.Summary = new TimesheetSummary
        {
            TotalWorkDays = result.DailyRecords.Count,
            TotalWorkedTime = totalWorked,
            TotalPlannedTime = totalPlanned,
            TotalBalance = totalPlanned > TimeSpan.Zero ? totalWorked - totalPlanned : TimeSpan.Zero,
            TotalLateCount = totalLate,
            TotalEarlyCount = totalEarly,
            TotalAlcotestViolations = totalAlco
        };

        return result;
    }
}

