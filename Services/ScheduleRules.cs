using LogGate.Models;

namespace LogGate.Services
{
    public static class ScheduleRules
    {
        public static List<DateTime> PreHolidays { get; private set; } = new();
        public static List<WorkScheduleRule> Rules { get; private set; } = new();

        public static bool IsEarlyDeparture(DataItem x)
        {
            if (Rules.Count == 0 || !x.EventTime.HasValue || x.Direction != "Выход") return false;

            var rule = Rules.FirstOrDefault(r => r.IsPersonal && r.TargetName == x.FullName) ??
                       Rules.FirstOrDefault(r => !r.IsPersonal && r.TargetName == x.Department);

            if (rule == null) return false;

            int normalEnd = (int)rule.EndTime.TotalMinutes;
            int shortEnd = normalEnd - 60;
            int eventMinutes = x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute;

            bool isShortDay = PreHolidays.Contains(x.EventTime.Value.Date);
            int limit = isShortDay ? shortEnd : normalEnd;

            return eventMinutes < limit;
        }

        public static bool IsLate(DataItem x)
        {
            if (Rules.Count == 0 || !x.EventTime.HasValue || x.Direction != "Вход") return false;

            var rule = Rules.FirstOrDefault(r => r.IsPersonal && r.TargetName == x.FullName) ??
                       Rules.FirstOrDefault(r => !r.IsPersonal && r.TargetName == x.Department);

            if (rule == null) return false;

            int startMinutes = (int)rule.StartTime.TotalMinutes;
            int eventMinutes = x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute;

            return eventMinutes > startMinutes + 1;
        }

        public static bool RequiresAlcotest(DataItem item)
        {
            var rule = Rules.FirstOrDefault(r => r.IsPersonal && r.TargetName == item.FullName) ??
                       Rules.FirstOrDefault(r => !r.IsPersonal && r.TargetName == item.Department);

            return rule?.RequiresAlcotest ?? false;
        }

        public static void UpdateRules(List<WorkScheduleRule> rules, List<DateTime> holidays)
        {
            Rules = rules;
            PreHolidays = holidays;
        }
    }
}