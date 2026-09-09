using LogGate.Models;
using System.Collections.Generic;

namespace LogGate.Interfaces
{
    public interface IDataCleaningService
    {
        List<DataItem> CleanAnomalies(List<DataItem> rawItems);
    }
}