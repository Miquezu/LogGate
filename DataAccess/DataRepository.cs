using LogGate.Interfaces;
using LogGate.Models;
using Microsoft.EntityFrameworkCore;

namespace LogGate.DataAccess
{
    internal class DataRepository : IDataRepository
    {
        private readonly AppDBContext _context;

        // Контейнер сам передаст сюда настроенный AppDBContext
        public DataRepository(AppDBContext context)
        {
            _context = context;
        }

        public IQueryable<DataItem> GetAllItems() => _context.DataItems;

        public IQueryable<WorkScheduleRule> GetAllSchedules() => _context.WorkScheduleRules;

        public async Task<List<WorkScheduleRule>> GetAllWorkRulesAsync() =>
            await _context.WorkScheduleRules.ToListAsync();

        public async Task<List<DateTime>> GetShortenedDaysByYearAsync(int year) =>
             await _context.ShortenedWorkDays
                .Where(x => x.Date.Year == year)
                .Select(x => x.Date)
                .ToListAsync();

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();

        public async Task<int> SaveItemsAsync(IEnumerable<DataItem> items)
        {
            var existingKeys = await _context.DataItems
                .Select(x => new { x.RecordNumber, x.EventTime })
                .ToListAsync();

            var existingHashSet = existingKeys
                .Select(x => (x.RecordNumber, x.EventTime))
                .ToHashSet();

            var filteredItems = items
                .Where(item => !existingHashSet.Contains((item.RecordNumber, item.EventTime)))
                .ToList();

            if (filteredItems.Count != 0)
            {
                await _context.DataItems.AddRangeAsync(filteredItems);
                await _context.SaveChangesAsync();
                return filteredItems.Count;
            }
            return 0;
        }

        public async Task SaveShortenedDaysAsync(IEnumerable<DateTime> dates)
        {
            var entities = dates.Select(d => new ShortenedWorkDay { Date = d });
            await _context.ShortenedWorkDays.AddRangeAsync(entities);
            await _context.SaveChangesAsync();
        }

        public async Task SaveWorkRulesAsync(IEnumerable<WorkScheduleRule> rules)
        {
            var existingRules = await _context.WorkScheduleRules.ToListAsync();
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
                await _context.WorkScheduleRules.AddRangeAsync(cleanRules);

            await _context.SaveChangesAsync();
        }
    }
}