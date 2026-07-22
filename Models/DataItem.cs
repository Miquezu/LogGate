using System;
using System.Collections.Generic;
using System.Text;

namespace LogGate.Models
{
    public class DataItem
    {
        // 1. Системный ключ для базы данных SQLite (EF Core сам заполнит его)
        public int Id { get; set; }

        // 2. Данные напрямую из вашего файла
        public string? RecordNumber { get; set; } // № п/п
        public string? Post { get; set; }       // Пост
        public DateTime? EventTime { get; set; }  // Время вх./вых.
        public string? Direction { get; set; }   // Вх./Вых. (Вход или Выход)

        public DateTime? TemperatureTime { get; set; } // Время темп-ры
        public double? Temperature { get; set; }       // Темп-ра,°C

        public DateTime? AlcotestTime { get; set; }  // Время алкотеста
        public double? AlcotestResult { get; set; }  // Результат,мг/л

        public string? FullName { get; set; } = string.Empty;      // Ф.И.О. тестируемого
        public string? Position { get; set; } = string.Empty;      // Должность
        public string? Department { get; set; } = string.Empty;    // Подразделение
        public string? EmployeeNumber { get; set; } = string.Empty;// Таб.№
        public string? PassNumber { get; set; } = string.Empty;    // Пропуск

    }
}
