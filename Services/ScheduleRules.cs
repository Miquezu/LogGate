using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LogGate.Services
{
    public static class ScheduleRules
    {
        public static List<DateTime> PreHolidays { get; private set; } = new();
        public static List<WorkScheduleRule> Rules { get; private set; } = new();

        private static Func<DataItem, bool>? _isLateFunc;
        private static Func<DataItem, bool>? _isEarlyDepartureFunc;

        // Метод для инициализации правил при старте приложения
        public static void UpdateRules(List<WorkScheduleRule> rules, List<DateTime> holidays)
        {
            Rules = rules;
            PreHolidays = holidays;

            // Перекомпилируем функции для UI (DataGrid) при обновлении базы
            _isLateFunc = IsLateExpression().Compile();
            _isEarlyDepartureFunc = IsEarlyDepartureExpression().Compile();
        }

        public static Expression<Func<DataItem, bool>> IsLateExpression()
        {
            var predicate = PredicateBuilder.False<DataItem>();
            if (Rules.Count == 0) return predicate; // Если правил нет, никто не опаздывает

            // Получаем список всех сотрудников с персональными графиками
            var personalExceptions = Rules.Where(r => r.IsPersonal).Select(r => r.TargetName).ToList();

            foreach (var rule in Rules)
            {
                int startMinutes = (int)rule.StartTime.TotalMinutes;

                if (rule.IsPersonal)
                {
                    predicate = predicate.Or(x =>
                        x.EventTime.HasValue &&
                        x.Direction == "Вход" &&
                        x.FullName == rule.TargetName &&
                        (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute > startMinutes + 1));
                }
                else
                {
                    // Для отделов исключаем тех сотрудников, у которых есть персональный график
                    predicate = predicate.Or(x =>
                        x.EventTime.HasValue &&
                        x.Direction == "Вход" &&
                        x.Department == rule.TargetName &&
                        !personalExceptions.Contains(x.FullName) &&
                        (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute > startMinutes + 1));
                }
            }

            return predicate;
        }

        public static Expression<Func<DataItem, bool>> IsEarlyDepartureExpression()
        {
            var predicate = PredicateBuilder.False<DataItem>();
            if (Rules.Count == 0) return predicate;

            var personalExceptions = Rules.Where(r => r.IsPersonal).Select(r => r.TargetName).ToList();

            foreach (var rule in Rules)
            {
                int normalEnd = (int)rule.EndTime.TotalMinutes;
                int shortEnd = normalEnd - 60; // Логика сокращенного предпраздничного дня

                if (rule.IsPersonal)
                {
                    predicate = predicate.Or(x =>
                        x.EventTime.HasValue &&
                        x.Direction == "Выход" &&
                        x.FullName == rule.TargetName &&
                        (
                            (PreHolidays.Contains(x.EventTime.Value.Date) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < shortEnd)) ||
                            (!PreHolidays.Contains(x.EventTime.Value.Date) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < normalEnd))
                        ));
                }
                else
                {
                    predicate = predicate.Or(x =>
                        x.EventTime.HasValue &&
                        x.Direction == "Выход" &&
                        x.Department == rule.TargetName &&
                        !personalExceptions.Contains(x.FullName) &&
                        (
                            (PreHolidays.Contains(x.EventTime.Value.Date) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < shortEnd)) ||
                            (!PreHolidays.Contains(x.EventTime.Value.Date) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < normalEnd))
                        ));
                }
            }

            return predicate;
        }

        // Публичные свойства для использования в AnomalyColorConverter
        public static Func<DataItem, bool> IsLate => _isLateFunc ??= IsLateExpression().Compile();

        public static Func<DataItem, bool> IsEarlyDeparture => _isEarlyDepartureFunc ??= IsEarlyDepartureExpression().Compile();
    }
}