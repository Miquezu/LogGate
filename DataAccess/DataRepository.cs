using LogGate.Interfaces;
using LogGate.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LogGate.DataAccess
{
    internal class DataRepository : IDataRepository
    {
        private readonly IDbContextFactory<AppDBContext> _contextFactory;

        public DataRepository(IDbContextFactory<AppDBContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<WorkScheduleRule>> GetAllWorkRulesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.WorkScheduleRules.AsNoTracking().ToListAsync();
        }

        public async Task<List<DataItem>> GetEmployeeHistoryAsync(string employeeName)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.DataItems
                .AsNoTracking()
                .Where(x => x.FullName == employeeName)
                .OrderByDescending(x => x.EventTime)
                .ToListAsync();
        }

        public async Task<List<DataItem>> GetFilteredLogsAsync(string? searchText, DateTime? startDate, DateTime? endDate)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.DataItems.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lowerText = searchText.ToLower();
                query = query.Where(x =>
                    (x.FullName != null && x.FullName.ToLower().Contains(lowerText)) ||
                    (x.PassNumber != null && x.PassNumber.ToLower().Contains(lowerText)) ||
                    (x.Department != null && x.Department.ToLower().Contains(lowerText))
                );
            }

            if (startDate.HasValue)
                query = query.Where(x => x.EventTime >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(x => x.EventTime <= endDate.Value.AddDays(1).AddTicks(-1));

            return await query.ToListAsync();
        }

        public async Task<List<DateTime>> GetShortenedDaysByYearAsync(int year)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ShortenedWorkDays
                .AsNoTracking()
                .Where(x => x.Date.Year == year)
                .Select(x => x.Date)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.DataItems.CountAsync();
        }

        public async Task SaveChangesAsync()
        {
            // Метод оставлен для совместимости редактирования примечаний
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.SaveChangesAsync();
        }

        public async Task<int> SaveItemsAsync(IEnumerable<DataItem> items)
        {
            var incomingList = items
                .Where(x => !string.IsNullOrEmpty(x.RecordNumber) && x.EventTime.HasValue)
                .ToList();

            if (incomingList.Count == 0) return 0;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var minDate = incomingList.Min(x => x.EventTime!.Value);
            var maxDate = incomingList.Max(x => x.EventTime!.Value);

            // Выборка ключей только в диапазоне дат входящего пакета вместо скачивания всей таблицы
            var existingKeys = await context.DataItems
                .AsNoTracking()
                .Where(x => x.EventTime >= minDate && x.EventTime <= maxDate)
                .Select(x => new { x.RecordNumber, x.EventTime })
                .ToListAsync();

            var existingHashSet = existingKeys
                .Select(x => (x.RecordNumber, x.EventTime))
                .ToHashSet();

            var filteredItems = incomingList
                .Where(item => !existingHashSet.Contains((item.RecordNumber, item.EventTime)))
                .ToList();

            if (filteredItems.Count != 0)
            {
                await context.DataItems.AddRangeAsync(filteredItems);
                await context.SaveChangesAsync();
                return filteredItems.Count;
            }
            return 0;
        }

        public async Task SaveShortenedDaysAsync(IEnumerable<DateTime> dates)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var entities = dates.Select(d => new ShortenedWorkDay { Date = d });
            await context.ShortenedWorkDays.AddRangeAsync(entities);
            await context.SaveChangesAsync();
        }

        public async Task SaveWorkRulesAsync(IEnumerable<WorkScheduleRule> rules)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingRules = await context.WorkScheduleRules.ToListAsync();
            if (existingRules.Any())
                context.WorkScheduleRules.RemoveRange(existingRules);

            var cleanRules = rules.Select(r => new WorkScheduleRule
            {
                TargetName = r.TargetName,
                IsPersonal = r.IsPersonal,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                RequiresAlcotest = r.RequiresAlcotest
            }).ToList();

            if (cleanRules.Any())
                await context.WorkScheduleRules.AddRangeAsync(cleanRules);

            await context.SaveChangesAsync();
        }
    }
}