namespace LogGate.Services;

/// <summary>
/// Реализация сервиса импорта данных СКУД.
/// Объединяет парсинг CSV, интеллектуальную очистку аномалий и сохранение в базу данных (DRY/SRP).
/// </summary>
public class DataImportService(
    IFileParser fileParser,
    IDataCleaningService cleaningService,
    IDataRepository dataRepository,
    ILogger<DataImportService> logger) : IDataImportService
{
    public async Task<int> ImportCsvAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fileName = Path.GetFileName(filePath);
        logger.LogInformation("Начат импорт файла {FileName} ({FilePath})", fileName, filePath);

        var rawData = fileParser.Parse(filePath);
        logger.LogInformation("Из файла {FileName} прочитано {RawCount} сырых записей.", fileName, rawData.Count);

        var cleanedData = cleaningService.CleanAnomalies(rawData);
        logger.LogInformation("После очистки аномалий сформировано {CleanCount} записей для сохранения.", cleanedData.Count);

        int addedCount = await dataRepository.SaveItemsAsync(cleanedData);
        logger.LogInformation("Импорт файла {FileName} завершен. Добавлено новых записей в БД: {AddedCount}.", fileName, addedCount);

        return addedCount;
    }
}

