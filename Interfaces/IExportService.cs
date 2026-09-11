namespace LogGate.Interfaces;

public interface IExportService
{
    Task ExportToCsvAsync(IEnumerable<DataItem> items, string filePath);

    Task ExportEmployeeTimesheetAsync(
        string employeeName,
        string department,
        string position,
        string employeeNumber,
        string punctualityRate,
        TimesheetSummary summary,
        IEnumerable<DailyWorkRecord> records,
        string filePath);
}

