using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogGate.Interfaces
{
    public interface IFileParser
    {
        List<DataItem> Parse(string filePath);
    }
}
