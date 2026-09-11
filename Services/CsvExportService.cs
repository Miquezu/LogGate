using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace LogGate.Services;

/// <summary>
/// Сервис экспорта аналитических данных и табелей в формат CSV.
/// </summary>
public class CsvExportService : IExportService
{
    public async Task ExportToCsvAsync(IEnumerable<DataItem> items, string filePath)
    {
        var config = new CsvConfiguration(new CultureInfo("ru-RU"))
        {
            Delimiter = ";",
            HasHeaderRecord = true
        };

        int index = 1;
        var records = items.Select(item => new ReportExportRecord
        {
            Number = index++,
            Date = item.EventTime?.ToString("dd.MM.yyyy") ?? string.Empty,
            EventTime = item.EventTime?.ToString("HH:mm:ss") ?? string.Empty,
            Direction = item.Direction ?? string.Empty,
            FullName = item.FullName ?? string.Empty,
            Department = item.Department ?? string.Empty,
            Position = item.Position ?? string.Empty,
            EmployeeNumber = item.EmployeeNumber ?? string.Empty,
            PassNumber = item.PassNumber ?? string.Empty,
            TemperatureTime = item.TemperatureTime?.ToString("HH:mm:ss") ?? string.Empty,
            Temperature = item.Temperature.HasValue ? item.Temperature.Value.ToString("F1", CultureInfo.InvariantCulture) : string.Empty,
            AlcotestTime = item.AlcotestTime?.ToString("HH:mm:ss") ?? string.Empty,
            AlcotestResult = item.AlcotestResult.HasValue ? item.AlcotestResult.Value.ToString("F2", CultureInfo.InvariantCulture) : string.Empty,
            IsLate = item.IsLate ? "Да" : "Нет",
            IsEarlyDeparture = item.IsEarlyDeparture ? "Да" : "Нет",
            AlcotestViolation = item.HasAlcotestViolation ? "Да" : "Нет",
            SystemNote = item.SystemNote ?? string.Empty,
            Note = item.Note ?? string.Empty
        }).ToList();

        // UTF-8 с BOM (true), чтобы Microsoft Excel в Windows корректно открывал кириллицу
        await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
        await using var csv = new CsvWriter(writer, config);

        csv.Context.RegisterClassMap<ReportExportRecordMap>();
        await csv.WriteRecordsAsync(records);
    }

    public async Task ExportEmployeeTimesheetAsync(
        string employeeName,
        string department,
        string position,
        string employeeNumber,
        string punctualityRate,
        TimesheetSummary summary,
        IEnumerable<DailyWorkRecord> records,
        string filePath)
    {
        var sb = new StringBuilder();

        // Шапка отчёта
        sb.AppendLine("Досье сотрудника и Табель учёта рабочего времени;");
        sb.AppendLine($"ФИО:;{employeeName}");
        sb.AppendLine($"Подразделение:;{department}");
        sb.AppendLine($"Должность:;{position}");
        sb.AppendLine($"Табельный номер:;{employeeNumber}");
        sb.AppendLine($"Дата выгрузки:;{DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine(";");

        // Сводный блок
        sb.AppendLine("СВОДНЫЕ ПОКАЗАТЕЛИ ЗА ПЕРИОД:;");
        sb.AppendLine($"Отработано рабочих дней:;{summary.TotalWorkDays}");
        sb.AppendLine($"Фактически отработано:;{summary.TotalWorkedTimeFormatted}");
        sb.AppendLine($"Норма по графику:;{summary.TotalPlannedTimeFormatted}");
        sb.AppendLine($"Баланс рабочего времени:;{summary.TotalBalanceFormatted}");
        sb.AppendLine($"Средняя смена в день:;{summary.AverageHoursPerDayFormatted}");
        sb.AppendLine($"Индекс соблюдения графика (пунктуальность):;{punctualityRate}");
        sb.AppendLine($"Зафиксировано опозданий:;{summary.TotalLateCount}");
        sb.AppendLine($"Зафиксировано ранних уходов:;{summary.TotalEarlyCount}");
        sb.AppendLine($"Нарушений алкотеста:;{summary.TotalAlcotestViolations}");
        sb.AppendLine(";");

        // Таблица по дням
        sb.AppendLine("ЕЖЕДНЕВНЫЙ ТАБЕЛЬ УЧЁТА ВРЕМЕНИ:;");
        sb.AppendLine("№;Дата;День недели;Первый вход;Последний выход;Число проходов;Отработано;Норма;Баланс;Опоздание;Ранний уход;Алкотест;Статус");

        int idx = 1;
        foreach (var r in records)
        {
            string dayOfWeek = r.Date.ToString("dddd", new CultureInfo("ru-RU"));
            string isLate = r.IsLate ? "Да" : "Нет";
            string isEarly = r.IsEarlyDeparture ? "Да" : "Нет";
            string hasAlco = r.HasAlcotestViolation ? "Да" : "Нет";

            sb.AppendLine($"{idx++};{r.DateFormatted};{dayOfWeek};{r.FirstEntryFormatted};{r.LastExitFormatted};{r.PassesCount};{r.WorkedTimeFormatted};{r.PlannedTimeFormatted};{r.BalanceFormatted};{isLate};{isEarly};{hasAlco};{r.StatusText}");
        }

        await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
        await writer.WriteAsync(sb.ToString());
    }

    private sealed class ReportExportRecord
    {
        public int Number { get; set; }
        public string Date { get; set; } = string.Empty;
        public string EventTime { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string PassNumber { get; set; } = string.Empty;
        public string TemperatureTime { get; set; } = string.Empty;
        public string Temperature { get; set; } = string.Empty;
        public string AlcotestTime { get; set; } = string.Empty;
        public string AlcotestResult { get; set; } = string.Empty;
        public string IsLate { get; set; } = string.Empty;
        public string IsEarlyDeparture { get; set; } = string.Empty;
        public string AlcotestViolation { get; set; } = string.Empty;
        public string SystemNote { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    private sealed class ReportExportRecordMap : ClassMap<ReportExportRecord>
    {
        public ReportExportRecordMap()
        {
            Map(m => m.Number).Name("№ п/п");
            Map(m => m.Date).Name("Дата");
            Map(m => m.EventTime).Name("Время");
            Map(m => m.Direction).Name("Вх./Вых.");
            Map(m => m.FullName).Name("ФИО");
            Map(m => m.Department).Name("Подразделение");
            Map(m => m.Position).Name("Должность");
            Map(m => m.EmployeeNumber).Name("Таб. №");
            Map(m => m.PassNumber).Name("Пропуск");
            Map(m => m.TemperatureTime).Name("Время темп-ры");
            Map(m => m.Temperature).Name("Темп-ра, °C");
            Map(m => m.AlcotestTime).Name("Время алкотеста");
            Map(m => m.AlcotestResult).Name("Алкотест, ‰");
            Map(m => m.IsLate).Name("Опоздание");
            Map(m => m.IsEarlyDeparture).Name("Ранний уход");
            Map(m => m.AlcotestViolation).Name("Нарушение алкотеста");
            Map(m => m.SystemNote).Name("Системный статус");
            Map(m => m.Note).Name("Примечание");
        }
    }
}

