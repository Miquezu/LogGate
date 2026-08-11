using LogGate.Models;

namespace LogGate.Interfaces
{
    public interface IDataRepository
    {
        int SaveItems(IEnumerable<DataItem> items);

        IQueryable<DataItem> GetAllItems();

        List<DateTime> GetShortenedDaysByYear(int year);

        void SaveShortenedDays(IEnumerable<DateTime> dates);

        List<WorkScheduleRule> GetAllWorkRules();

        void SaveWorkRules(IEnumerable<WorkScheduleRule> rules);
    }
}