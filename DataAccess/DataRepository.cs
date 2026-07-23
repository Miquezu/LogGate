using LogGate.Interfaces;
using LogGate.Models;

namespace LogGate.DataAccess
{
    internal class DataRepository : IDataRepository
    {
        public int SaveItems(IEnumerable<DataItem> items)
        {
            using var context = new AppDBContext();

            // 1. Формируем составной ключ из сущностей в БД: RecordNumber + EventTime
            var existingKeys = context.DataItems
                .Select(x => x.RecordNumber + "_" + x.EventTime.ToString())
                .ToHashSet();

            // 2. Фильтруем новые элементы по такому же составному ключу
            var filteredItems = items
                .Where(item => !existingKeys.Contains($"{item.RecordNumber}_{item.EventTime}"))
                .ToList();

            if (filteredItems.Count != 0)
            {
                context.DataItems.AddRange(filteredItems);
                context.SaveChanges();
                return filteredItems.Count;
            }

            return 0;
        }

        public List<DataItem> GetAllItems()
        {
            using var context = new AppDBContext();
            return context.DataItems.ToList();
        }
    }
}
