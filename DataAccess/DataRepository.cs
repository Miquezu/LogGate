using LogGate.Interfaces;
using LogGate.Models;
using Microsoft.EntityFrameworkCore;

namespace LogGate.DataAccess
{
    internal class DataRepository : IDataRepository, IDisposable
    {
        private readonly AppDBContext _context;

        public DataRepository()
        {
            _context = new AppDBContext();
        }

        public int SaveItems(IEnumerable<DataItem> items)
        {
            var existingKeys = _context.DataItems
                .Select(x => new { x.RecordNumber, x.EventTime })
                .AsEnumerable()
                .Select(x => (x.RecordNumber, x.EventTime))
                .ToHashSet();

            var filteredItems = items
                .Where(item => !existingKeys.Contains((item.RecordNumber, item.EventTime)))
                .ToList();

            if (filteredItems.Count != 0)
            {
                _context.DataItems.AddRange(filteredItems);
                _context.SaveChanges();
                return filteredItems.Count;
            }
            return 0;
        }

        public IQueryable<DataItem> GetAllItems()
        {
            return _context.DataItems.AsNoTracking();
        }

        public void Dispose() =>
            _context?.Dispose();
    }
}