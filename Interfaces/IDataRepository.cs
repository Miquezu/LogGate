using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogGate.Interfaces
{
    public interface IDataRepository
    {
        void SaveItems(IEnumerable<DataItem> items);
        List<DataItem> GetAllItems();
    }
}
