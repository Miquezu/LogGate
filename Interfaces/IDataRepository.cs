using LogGate.Models;

namespace LogGate.Interfaces
{
    public interface IDataRepository
    {
        int SaveItems(IEnumerable<DataItem> items);

        IQueryable<DataItem> GetAllItems();
    }
}