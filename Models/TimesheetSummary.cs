using System;

namespace LogGate.Models
{
    public class TimesheetSummary
    {
        public int TotalWorkDays { get; set; }

        public TimeSpan TotalWorkedTime { get; set; }

        public string TotalWorkedTimeFormatted
        {
            get
            {
                int hours = (int)TotalWorkedTime.TotalHours;
                int minutes = TotalWorkedTime.Minutes;
                return $"{hours} ч {minutes:D2} мин";
            }
        }

        public TimeSpan TotalPlannedTime { get; set; }

        public string TotalPlannedTimeFormatted
        {
            get
            {
                if (TotalPlannedTime <= TimeSpan.Zero) return "—";
                int hours = (int)TotalPlannedTime.TotalHours;
                int minutes = TotalPlannedTime.Minutes;
                return $"{hours} ч {minutes:D2} мин";
            }
        }

        public TimeSpan TotalBalance { get; set; }

        public string TotalBalanceFormatted
        {
            get
            {
                if (TotalPlannedTime <= TimeSpan.Zero) return "—";

                int totalMinutes = (int)TotalBalance.TotalMinutes;
                string sign = totalMinutes >= 0 ? "+" : "-";
                int absMinutes = Math.Abs(totalMinutes);
                int hours = absMinutes / 60;
                int minutes = absMinutes % 60;

                if (hours > 0)
                    return $"{sign}{hours} ч {minutes:D2} мин";

                return $"{sign}{minutes} мин";
            }
        }

        public bool IsTotalOvertime => TotalPlannedTime > TimeSpan.Zero && TotalBalance.TotalMinutes > 0;

        public bool IsTotalUndertime => TotalPlannedTime > TimeSpan.Zero && TotalBalance.TotalMinutes < 0;

        public double AverageHoursPerDay => TotalWorkDays > 0 ? TotalWorkedTime.TotalHours / TotalWorkDays : 0.0;

        public string AverageHoursPerDayFormatted
        {
            get
            {
                int hours = (int)AverageHoursPerDay;
                int minutes = (int)Math.Round((AverageHoursPerDay - hours) * 60);
                return $"{hours} ч {minutes:D2} мин";
            }
        }

        public int TotalLateCount { get; set; }

        public int TotalEarlyCount { get; set; }

        public int TotalAlcotestViolations { get; set; }
    }
}

