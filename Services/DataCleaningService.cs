using LogGate.Interfaces;
using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LogGate.Services
{
    public class DataCleaningService : IDataCleaningService
    {
        private const string DirectionIn = "Вход";
        private const string DirectionOut = "Выход";
        private const string NoteAutoEvening = "Автоисправление (вечер)";
        private const string NoteAutoMorning = "Автоисправление (утро)";
        private const string NoteMissingPass = "Аномалия СКУД: Пропущен проход";
        private const string NoteTechCard = "Технический пропуск";
        private const string SecurityDepartmentKeyword = "охраны";
        private const string SecurityPositionKeyword = "контролер на";

        public List<DataItem> CleanAnomalies(List<DataItem> rawItems)
        {
            var cleaned = new List<DataItem>();

            var validItems = rawItems
                .Where(x => !string.IsNullOrWhiteSpace(x.FullName) && x.EventTime.HasValue)
                .ToList();

            var invalidItems = rawItems.Except(validItems).ToList();

            var grouped = validItems
                .OrderBy(x => x.EventTime)
                .GroupBy(x => !string.IsNullOrWhiteSpace(x.EmployeeNumber) ? x.EmployeeNumber : x.FullName);

            foreach (var group in grouped)
            {
                var logs = group.OrderBy(x => x.EventTime).ToList();

                for (int i = 0; i < logs.Count; i++)
                {
                    var current = logs[i];

                    bool isSecurity = (current.Department != null && current.Department.Contains(SecurityDepartmentKeyword, StringComparison.OrdinalIgnoreCase)) ||
                                      (current.Position != null && current.Position.Contains(SecurityPositionKeyword, StringComparison.OrdinalIgnoreCase));

                    bool isTechnicalCard = current.FullName!.StartsWith('{') && current.FullName.EndsWith('}');
                    if (isTechnicalCard && string.IsNullOrWhiteSpace(current.SystemNote))
                    {
                        current.SystemNote = NoteTechCard;
                    }

                    if (cleaned.Count > 0 && cleaned.Last().FullName == current.FullName)
                    {
                        var previous = cleaned.Last();
                        TimeSpan diff = current.EventTime!.Value - previous.EventTime!.Value;

                        // 1. Устранение аппаратного дребезга (< 2 минут) со слиянием замеров
                        if (diff.TotalMinutes < 2)
                        {
                            if (previous.Direction != current.Direction)
                            {
                                MergeMeasurements(current, previous);
                                cleaned.RemoveAt(cleaned.Count - 1);
                                cleaned.Add(current);
                                continue;
                            }
                            else
                            {
                                if (string.Equals(current.Direction, DirectionIn, StringComparison.OrdinalIgnoreCase))
                                {
                                    MergeMeasurements(previous, current);
                                    continue;
                                }

                                if (string.Equals(current.Direction, DirectionOut, StringComparison.OrdinalIgnoreCase))
                                {
                                    MergeMeasurements(current, previous);
                                    cleaned.RemoveAt(cleaned.Count - 1);
                                    cleaned.Add(current);
                                    continue;
                                }
                            }
                        }

                        // 2. Автоисправление направлений (только для обычного персонала с дневными сменами)
                        bool isFirstEventToday = previous.EventTime!.Value.Date != current.EventTime!.Value.Date;
                        int hour = current.EventTime.Value.Hour;

                        if (!isSecurity && !isTechnicalCard)
                        {
                            if (isFirstEventToday && string.Equals(current.Direction, DirectionOut, StringComparison.OrdinalIgnoreCase) && hour < 12)
                            {
                                current.Direction = DirectionIn;
                                current.SystemNote = NoteAutoMorning;
                            }
                            else if (!isFirstEventToday &&
                                     string.Equals(previous.Direction, DirectionIn, StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(current.Direction, DirectionIn, StringComparison.OrdinalIgnoreCase) &&
                                     hour >= 15)
                            {
                                current.Direction = DirectionOut;
                                current.SystemNote = NoteAutoEvening;
                            }
                        }

                        // 3. Фиксация разрыва последовательности при совпадении направлений
                        if (previous.Direction == current.Direction)
                        {
                            current.SystemNote = NoteMissingPass;
                        }
                    }
                    else
                    {
                        // Первая запись сотрудника в выборке
                        if (!isSecurity && !isTechnicalCard &&
                            string.Equals(current.Direction, DirectionOut, StringComparison.OrdinalIgnoreCase) &&
                            current.EventTime!.Value.Hour < 12)
                        {
                            current.Direction = DirectionIn;
                            current.SystemNote = NoteAutoMorning;
                        }
                    }

                    cleaned.Add(current);
                }
            }

            cleaned.AddRange(invalidItems);
            return cleaned.OrderBy(x => x.EventTime).ToList();
        }

        private static void MergeMeasurements(DataItem target, DataItem source)
        {
            if (!target.Temperature.HasValue && source.Temperature.HasValue)
            {
                target.Temperature = source.Temperature;
                target.TemperatureTime = source.TemperatureTime;
            }

            if (!target.AlcotestResult.HasValue && source.AlcotestResult.HasValue)
            {
                target.AlcotestResult = source.AlcotestResult;
                target.AlcotestTime = source.AlcotestTime;
            }
        }
    }
}