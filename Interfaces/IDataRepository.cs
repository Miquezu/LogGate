using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LogGate.Interfaces
{
    public interface IDataRepository
    {
        Task<List<WorkScheduleRule>> GetAllWorkRulesAsync();

        Task<List<DataItem>> GetEmployeeHistoryAsync(string employeeName);

        Task<List<DataItem>> GetFilteredLogsAsync(string? searchText, DateTime? startDate, DateTime? endDate);

        Task<List<DateTime>> GetShortenedDaysByYearAsync(int year);

        Task<int> GetTotalCountAsync();

        Task<int> SaveItemsAsync(IEnumerable<DataItem> items);

        Task SaveShortenedDaysAsync(IEnumerable<DateTime> dates);

        Task SaveWorkRulesAsync(IEnumerable<WorkScheduleRule> rules);

        Task UpdateItemsNotesAsync(IEnumerable<DataItem> items);
    }
}