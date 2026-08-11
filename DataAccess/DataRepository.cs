using LogGate.Interfaces;
using LogGate.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public List<DateTime> GetShortenedDaysByYear(int year)
        {
            return _context.ShortenedWorkDays
                .Where(x => x.Date.Year == year)
                .Select(x => x.Date)
                .ToList();
        }

        public void SaveShortenedDays(IEnumerable<DateTime> dates)
        {
            var entities = dates.Select(d => new ShortenedWorkDay { Date = d });
            _context.ShortenedWorkDays.AddRange(entities);
            _context.SaveChanges();
        }

        public List<WorkScheduleRule> GetAllWorkRules()
        {
            return _context.WorkScheduleRules.ToList();
        }

        public void SaveWorkRules(IEnumerable<WorkScheduleRule> rules)
        {
            _context.WorkScheduleRules.RemoveRange(_context.WorkScheduleRules);
            _context.WorkScheduleRules.AddRange(rules);
            _context.SaveChanges();
        }

        public void Dispose() =>
            _context?.Dispose();
    }
}