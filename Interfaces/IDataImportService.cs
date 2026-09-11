namespace LogGate.Interfaces;

/// <summary>
/// Сервис импорта данных СКУД из CSV-файлов (парсинг, очистка аномалий и сохранение в БД).
/// </summary>
public interface IDataImportService
{
    /// <summary>
    /// Выполняет сквозной импорт данных из указанного CSV-файла.
    /// Возвращает количество успешно добавленных новых записей.
    /// </summary>
    Task<int> ImportCsvAsync(string filePath, CancellationToken ct = default);
}

