namespace LogGate.Interfaces;

public interface IDataCleaningService
{
    List<DataItem> CleanAnomalies(List<DataItem> rawItems);
}
