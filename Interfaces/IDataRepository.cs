using LogGate.Models;

namespace LogGate.Interfaces
{
    public interface IDataRepository
    {
        IQueryable<DataItem> GetAllItems();

        List<WorkScheduleRule> GetAllWorkRules();

        List<DateTime> GetShortenedDaysByYear(int year);

        void SaveChanges();

        int SaveItems(IEnumerable<DataItem> items);

        void SaveShortenedDays(IEnumerable<DateTime> dates);

        void SaveWorkRules(IEnumerable<WorkScheduleRule> rules);
    }
}