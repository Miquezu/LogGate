using LogGate.Interfaces;
using LogGate.Models;

namespace LogGate.DataAccess
{
    internal class DataRepository : IDataRepository
    {
        public int SaveItems(IEnumerable<DataItem> items)
        {
            using var context = new AppDBContext();

            var existingKeys = context.DataItems
                .Select(x => new { x.RecordNumber, x.EventTime })
                .AsEnumerable()
                .Select(x => (x.RecordNumber, x.EventTime))
                .ToHashSet();

            var filteredItems = items
                .Where(item => !existingKeys.Contains((item.RecordNumber, item.EventTime)))
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