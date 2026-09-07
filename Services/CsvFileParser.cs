using CsvHelper;
using CsvHelper.Configuration;
using LogGate.Interfaces;
using LogGate.Models;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

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
                BadDataFound = null
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<DataItemMap>();

            var rawRecords = csv.GetRecords<DataItem>().ToList();
            return CleanAnomalies(rawRecords); // Пропускаем через фильтр
        }

        private List<DataItem> CleanAnomalies(List<DataItem> rawItems)
        {
            var cleaned = new List<DataItem>();

            var validItems = rawItems.Where(x => !string.IsNullOrEmpty(x.FullName) && x.EventTime.HasValue).ToList();
            var invalidItems = rawItems.Except(validItems).ToList();

            var grouped = validItems
                .OrderBy(x => x.FullName)
                .ThenBy(x => x.EventTime)
                .GroupBy(x => x.FullName);

            foreach (var group in grouped)
            {
                var logs = group.ToList();
                for (int i = 0; i < logs.Count; i++)
                {
                    var current = logs[i];

                    // Исключение для ночных смен
                    bool isSecurity = current.Department != null &&
                                      current.Department.Contains("охраны", StringComparison.OrdinalIgnoreCase);

                    if (cleaned.Count > 0 && cleaned.Last().FullName == current.FullName)
                    {
                        var previous = cleaned.Last();
                        TimeSpan diff = current.EventTime.Value - previous.EventTime.Value;

                        // УРОВЕНЬ 1: Быстрые ошибки и дребезг (до 2 минут)
                        if (diff.TotalMinutes < 2)
                        {
                            if (previous.Direction != current.Direction)
                            {
                                cleaned.RemoveAt(cleaned.Count - 1); // Оставляем только последнее (исправленное) действие
                                cleaned.Add(current);
                                continue;
                            }
                            else
                            {
                                if (current.Direction == "Вход") continue; // Оставляем первый Вход
                                if (current.Direction == "Выход")
                                {
                                    cleaned.RemoveAt(cleaned.Count - 1); // Оставляем последний Выход
                                    cleaned.Add(current);
                                    continue;
                                }
                            }
                        }

                        // УРОВЕНЬ 2: Умное автоисправление (только для дневного персонала)
                        bool isFirstEventToday = previous.EventTime.Value.Date != current.EventTime.Value.Date;
                        int hour = current.EventTime.Value.Hour;

                        if (!isSecurity)
                        {
                            if (isFirstEventToday && current.Direction == "Выход" && hour < 12)
                            {
                                current.Direction = "Вход";
                                current.SystemNote = "Автоисправление (утро)";
                            }
                            else if (!isFirstEventToday && previous.Direction == "Вход" && current.Direction == "Вход" && hour >= 15)
                            {
                                current.Direction = "Выход";
                                current.SystemNote = "Автоисправление (вечер)";
                            }
                        }

                        // УРОВЕНЬ 3: Долгие пропуски
                        if (previous.Direction == current.Direction)
                            current.SystemNote = "Аномалия СКУД: Пропущен проход";
                    }
                    else
                    {
                        // Проверка самой первой записи сотрудника в файле
                        if (!isSecurity && current.Direction == "Выход" && current.EventTime.Value.Hour < 12)
                        {
                            current.Direction = "Вход";
                            current.SystemNote = "Автоисправление (утро)";
                        }
                    }
                    cleaned.Add(current);
                }
            }
            cleaned.AddRange(invalidItems);
            return cleaned.OrderBy(x => x.EventTime).ToList();
        }
    }
}