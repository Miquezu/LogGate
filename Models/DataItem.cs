namespace LogGate.Models
{
    public class DataItem
    {
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

        public string? FullName { get; set; }      // Ф.И.О. тестируемого
        public string? Position { get; set; }     // Должность
        public string? Department { get; set; }     // Подразделение
        public string? EmployeeNumber { get; set; } // Таб.№
        public string? PassNumber { get; set; }     // Пропуск

    }
}
