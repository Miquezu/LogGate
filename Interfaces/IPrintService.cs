using LogGate.Models;
using System.Collections.Generic;

namespace LogGate.Interfaces
{
    public interface IPrintService
    {
        bool PrintEmployeeDossier(
            string employeeName,
            string department,
            string position,
            string employeeNumber,
            string punctualityRate,
            int lateCount,
            int earlyCount,
            TimesheetSummary summary,
            IEnumerable<DailyWorkRecord> records);
    }
}

