using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using LogGate.Interfaces;
using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

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
                MissingFieldFound = null,
                BadDataFound = null,
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);

            csv.Context.TypeConverterOptionsCache.GetOptions<double?>().NumberStyles = NumberStyles.Float | NumberStyles.AllowDecimalPoint;
            csv.Context.TypeConverterCache.AddConverter<double?>(new FlexibleDoubleConverter());

            csv.Context.RegisterClassMap<DataItemMap>();

            return csv.GetRecords<DataItem>().ToList();
        }

        private sealed class FlexibleDoubleConverter : DefaultTypeConverter
        {
            public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;

                string normalized = text.Replace(',', '.').Trim();
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)) return result;

                return null;
            }
        }
    }
}