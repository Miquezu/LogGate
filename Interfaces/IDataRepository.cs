using LogGate.Models;

namespace LogGate.Interfaces
{
    public interface IDataRepository
    {
        IQueryable<DataItem> GetAllItems();

        Task<List<WorkScheduleRule>> GetAllWorkRulesAsync();

        Task<List<DateTime>> GetShortenedDaysByYearAsync(int year);

        Task SaveChangesAsync();

        Task<int> SaveItemsAsync(IEnumerable<DataItem> items);

        Task SaveShortenedDaysAsync(IEnumerable<DateTime> dates);

        Task SaveWorkRulesAsync(IEnumerable<WorkScheduleRule> rules);
    }
}