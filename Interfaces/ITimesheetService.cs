using LogGate.Models;
using System;
using System.Collections.Generic;

namespace LogGate.Interfaces
{
    public interface ITimesheetService
    {
        TimesheetResult CalculateTimesheet(IEnumerable<DataItem> employeeEvents, WorkScheduleRule? rule, IEnumerable<DateTime>? preHolidays = null);
    }
}

