using System.Collections.Generic;

namespace LogGate.Models
{
    public class TimesheetResult
    {
        public List<DailyWorkRecord> DailyRecords { get; set; } = [];

        public TimesheetSummary Summary { get; set; } = new();
    }
}
