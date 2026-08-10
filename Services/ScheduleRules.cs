using LogGate.Models;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace LogGate.Services
{
    public static class ScheduleRules
    {
        private static readonly string[] Shift730 = { "Ремонтно-механическая служба", "Слесарно-механическая мастерская", "Служба подготовки производства", "Столярный участок", "Участок машинной вышывки", "Участок пошива", "Участок раскроя", "Участок стёганных изделий", "Участок сувениров" };

        private static readonly string[] Shift800 = { "Администрация", "Специалисты", };

        private static readonly string[] Shift1000 = { "Магазин", };

        /// <summary>
        /// Фильтр для поиска ОПОЗДАНИЙ (Вход позже положенного времени)
        /// </summary>
        public static Expression<Func<DataItem, bool>> IsLateExpression()
        {
            return x => x.EventTime.HasValue && x.Direction == "Вход" &&
            (
                // Опоздания группы 07:30 (> 450 минут)
                (Shift730.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute > 451))
                ||
                // Опоздания группы 08:00 (> 480 минут)
                (Shift800.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute > 481))
                ||
                (Shift1000.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute > 601))
            );
        }

        public static Expression<Func<DataItem, bool>> IsEarlyDepartureExpression()
        {
            return x => x.EventTime.HasValue && x.Direction == "Выход" &&
            (
                (Shift730.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < 939))
                ||
                (Shift800.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < 969))
                ||
                (Shift1000.Contains(x.Department) && (x.EventTime.Value.Hour * 60 + x.EventTime.Value.Minute < 1080))
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