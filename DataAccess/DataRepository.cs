using LogGate.Interfaces;
using LogGate.Models;
using Microsoft.EntityFrameworkCore;

namespace LogGate.DataAccess
{
    internal class DataRepository : IDataRepository, IDisposable
    {
        private readonly AppDBContext _context;

        // Контейнер сам передаст сюда настроенный AppDBContext
        public DataRepository(AppDBContext context)
        {
            _context = context;
        }

        public void Dispose() => _context?.Dispose();

        public IQueryable<DataItem> GetAllItems() => _context.DataItems;

        public IQueryable<WorkScheduleRule> GetAllSchedules() => _context.WorkScheduleRules;

        public List<WorkScheduleRule> GetAllWorkRules() => _context.WorkScheduleRules.ToList();

        public List<DateTime> GetShortenedDaysByYear(int year) =>
             _context.ShortenedWorkDays
                .Where(x => x.Date.Year == year)
                .Select(x => x.Date)
                .ToList();

        public void SaveChanges() => _context.SaveChanges();

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

        public void SaveShortenedDays(IEnumerable<DateTime> dates)
        {
            var entities = dates.Select(d => new ShortenedWorkDay { Date = d });
            _context.ShortenedWorkDays.AddRange(entities);
            _context.SaveChanges();
        }

        public void SaveWorkRules(IEnumerable<WorkScheduleRule> rules)
        {
            var existingRules = _context.WorkScheduleRules.ToList();
            if (existingRules.Any())
                _context.WorkScheduleRules.RemoveRange(existingRules);

            var cleanRules = rules.Select(r => new WorkScheduleRule
            {
                TargetName = r.TargetName,
                IsPersonal = r.IsPersonal,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                RequiresAlcotest = r.RequiresAlcotest
            }).ToList();

            if (cleanRules.Any())
                _context.WorkScheduleRules.AddRange(cleanRules);
            _context.SaveChanges();
        }
    }
}