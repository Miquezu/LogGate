using LogGate.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LogGate.Interfaces
{
    public interface IExportService
    {
        Task ExportToCsvAsync(IEnumerable<DataItem> items, string filePath);
    }
}

