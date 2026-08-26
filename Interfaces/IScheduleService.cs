using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogGate.Interfaces
{
    public interface IScheduleService
    {
        List<DateTime> PreHolidays { get; }
        List<WorkScheduleRule> Rules { get; }

        bool IsEarlyDeparture(DataItem x);

        bool IsLate(DataItem x);

        bool RequiresAlcotest(DataItem item);

        void UpdateRules(List<WorkScheduleRule> rules, List<DateTime> holidays);
    }
}