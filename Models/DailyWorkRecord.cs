using System;

namespace LogGate.Models
{
    public class DailyWorkRecord
    {
        public DateTime Date { get; set; }

        public string DateFormatted => Date.ToString("dd.MM.yyyy");

        public string DayOfWeekFormatted
        {
            get
            {
                var dow = Date.ToString("ddd", new System.Globalization.CultureInfo("ru-RU"));
                return $"{Date:dd.MM.yyyy} ({char.ToUpper(dow[0]) + dow[1..]})";
            }
        }

        public TimeSpan? FirstEntry { get; set; }

        public string FirstEntryFormatted => FirstEntry.HasValue ? FirstEntry.Value.ToString(@"hh\:mm") : "—";

        public TimeSpan? LastExit { get; set; }

        public string LastExitFormatted => LastExit.HasValue ? LastExit.Value.ToString(@"hh\:mm") : "—";

        public int PassesCount { get; set; }

        public TimeSpan WorkedTime { get; set; }

        public string WorkedTimeFormatted
        {
            get
            {
                int hours = (int)WorkedTime.TotalHours;
                int minutes = WorkedTime.Minutes;
                return $"{hours} ч {minutes:D2} мин";
            }
        }

        public TimeSpan PlannedTime { get; set; }

        public string PlannedTimeFormatted
        {
            get
            {
                if (PlannedTime <= TimeSpan.Zero) return "—";
                int hours = (int)PlannedTime.TotalHours;
                int minutes = PlannedTime.Minutes;
                return $"{hours} ч {minutes:D2} мин";
            }
        }

        public TimeSpan Balance { get; set; }

        public string BalanceFormatted
        {
            get
            {
                if (PlannedTime <= TimeSpan.Zero) return "—";

                int totalMinutes = (int)Balance.TotalMinutes;
                string sign = totalMinutes >= 0 ? "+" : "-";
                int absMinutes = Math.Abs(totalMinutes);
                int hours = absMinutes / 60;
                int minutes = absMinutes % 60;

                if (hours > 0)
                    return $"{sign}{hours} ч {minutes:D2} мин";

                return $"{sign}{minutes} мин";
            }
        }

        public bool IsOvertime => PlannedTime > TimeSpan.Zero && Balance.TotalMinutes > 0;

        public bool IsUndertime => PlannedTime > TimeSpan.Zero && Balance.TotalMinutes < 0;

        public bool IsOnSchedule => PlannedTime > TimeSpan.Zero && (int)Balance.TotalMinutes == 0;

        public bool HasPlan => PlannedTime > TimeSpan.Zero;

        public bool IsLate { get; set; }

        public bool IsEarlyDeparture { get; set; }

        public bool HasAlcotestViolation { get; set; }

        public bool IsShortenedDay { get; set; }

        public string StatusText
        {
            get
            {
                if (HasAlcotestViolation) return "Алкотест!";
                if (IsLate && IsEarlyDeparture) return "Опоздание + Ранний уход";
                if (IsLate) return "Опоздание";
                if (IsEarlyDeparture) return "Ранний уход";
                if (IsOvertime) return "Переработка";
                if (IsUndertime) return "Недоработка";
                if (HasPlan) return "Норма";
                return "Вне графика";
            }
        }
    }
}
