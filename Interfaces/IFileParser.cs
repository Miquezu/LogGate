namespace LogGate.Interfaces;

public interface IFileParser
{
    List<DataItem> Parse(string filePath);
}
