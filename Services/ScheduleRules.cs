using LogGate.Models;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace LogGate.Services
{
    public static class ScheduleRules
    {
        // Группа 1: Приходят к 07:30, уходят в 16:30
        // 07:30 = 450 минут, 16:00 = 990 минут
        private static readonly string[] Shift730 = { "Ремонтно-механическая служба", "Слесарно-механическая мастерская", "Служба подготовки производства", "Столярный участок", "Участок машинной вышывки", "Участок пошива", "Участок раскроя", "Участок стёганных изделий", "Участок сувениров" };

        // Группа 2: Приходят к 09:00, уходят в 18:00
        // 09:00 = 540 минут, 18:00 = 1080 минут
        private static readonly string[] Shift900 = { "Администрация", "Специалисты", };

        // Группа 2: Приходят к 09:00, уходят в 18:00
        // 09:00 = 540 минут, 18:00 = 1080 минут
        private static readonly string[] Shift = { "Администрация", "Специалисты", };

        // СТАНДАРТНЫЙ ГРАФИК (все остальные подразделения)
        // Приходят к 08:00, уходят в 17:00
        // 08:00 = 480 минут, 17:00 = 1020 минут

        /// <summary>
        /// Фильтр для поиска ОПОЗДАНИЙ (Вход позже положенного времени)
        /// </summary>
        public static Expression<Func<DataItem, bool>> IsLateExpression()
        {
            return x => x.EventTime.HasValue && x.Direction == "Вход" &&
            (
                // Опоздания группы 07:30 (> 450 минут)
                (Shift730.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute > 450))
                ||
                // Опоздания группы 09:00 (> 540 минут)
                (Shift900.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute > 540))
                ||
                // Опоздания всех остальных (> 480 минут)
                (!Shift730.Contains(x.Department) && !Shift900.Contains(x.Department) &&
                (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute > 480))
            );
        }

        public static Expression<Func<DataItem, bool>> IsEarlyDepartureExpression()
        {
            return x => x.EventTime.HasValue && x.Direction == "Выход" &&
            (
                // Ранние уходы группы 07:30 (< 990 минут)
                (Shift730.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < 990))
                ||
                // Ранние уходы группы 09:00 (< 1080 минут)
                (Shift900.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < 1080))
                ||
                // Ранние уходы всех остальных (< 1020 минут)
                (!Shift730.Contains(x.Department) && !Shift900.Contains(x.Department) &&
                (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < 1020))
            );
        }

        private static Func<DataItem, bool> _isLateFunc;

        /// <summary>
        /// Функция проверки на опоздание (выполняется в оперативной памяти для UI)
        /// </summary>
        public static Func<DataItem, bool> IsLate => _isLateFunc ??= IsLateExpression().Compile();

        private static Func<DataItem, bool> _isEarlyDepartureFunc;

        /// <summary>
        /// Функция проверки на ранний уход (выполняется в оперативной памяти для UI)
        /// </summary>
        public static Func<DataItem, bool> IsEarlyDeparture => _isEarlyDepartureFunc ??= IsEarlyDepartureExpression().Compile();
    }
}