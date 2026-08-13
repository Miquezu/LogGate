using CsvHelper;
using CsvHelper.Configuration;
using LogGate.Interfaces;
using LogGate.Models;
using System.Globalization;
using System.IO;

namespace LogGate.Services
{
    public class CsvFileParser : IFileParser
    {
        public List<DataItem> Parse(string filePath)
        {
            var config = new CsvConfiguration(new CultureInfo("ru-RU"))
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                MissingFieldFound = null, // Не ругаться, если ячейка пустая
                BadDataFound = null       // Пропускать "битые" строки
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<DataItemMap>();
            var records = csv.GetRecords<DataItem>().ToList();

            return records;
        }
    }
}