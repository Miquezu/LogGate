using CsvHelper;
using CsvHelper.Configuration;
using LogGate.Interfaces;
using LogGate.Models;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogGate.Services
{
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
}

