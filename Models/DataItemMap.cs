using CsvHelper.Configuration;

namespace LogGate.Models
{
    public sealed class DataItemMap: ClassMap<DataItem>
    {
        public DataItemMap() {
            Map(m => m.Id).Ignore();

            Map(m => m.RecordNumber).Name("№ п/п");
            Map(m => m.Post).Name("Пост");
            Map(m => m.EventTime).Name("Время вх./вых.");
            Map(m => m.Direction).Name("Вх./Вых.");
            Map(m => m.TemperatureTime).Name("Время темп-ры");
            Map(m => m.Temperature).Name("Темп-ра,°C");
            Map(m => m.AlcotestTime).Name("Время алкотеста");
            Map(m => m.AlcotestResult).Name("Результат,мг/л");
            Map(m => m.FullName).Name("Ф.И.О. тестируемого");
            Map(m => m.Position).Name("Должность");
            Map(m => m.Department).Name("Подразделение");
            Map(m => m.EmployeeNumber).Name("Таб.№");
            Map(m => m.PassNumber).Name("Пропуск");
        }
    }
}
