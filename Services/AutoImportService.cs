using System.Threading.Channels;

namespace LogGate.Services;

/// <summary>
/// Фоновый сервис автоматического импорта файлов CSV из каталога AutoImport.
/// </summary>
public class AutoImportService : IDisposable
{
    private readonly IDataImportService _dataImportService;
    private readonly ILogger<AutoImportService>? _logger;
    private readonly string _importPath;
    private readonly string _archivePath;
    private readonly string _errorPath;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<string> _fileQueue = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly FileSystemWatcher _watcher;

    public AutoImportService(
        IDataImportService dataImportService,
        ILogger<AutoImportService>? logger = null)
    {
        _dataImportService = dataImportService;
        _logger = logger;

        _importPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoImport");
        _archivePath = Path.Combine(_importPath, "Archive");
        _errorPath = Path.Combine(_importPath, "Errors");

        Directory.CreateDirectory(_importPath);
        Directory.CreateDirectory(_archivePath);
        Directory.CreateDirectory(_errorPath);

        // Запуск последовательного фонового обработчика очереди
        Task.Run(() => ProcessQueueAsync(_cts.Token));

        // Постановка уже существующих файлов в очередь
        string[] existingFiles = Directory.GetFiles(_importPath, "*.csv");
        foreach (var file in existingFiles)
        {
            _fileQueue.Writer.TryWrite(file);
        }

        _watcher = new FileSystemWatcher(_importPath, "*.csv")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += (s, e) => _fileQueue.Writer.TryWrite(e.FullPath);
    }

    public event Action<int>? DataImported;
    public event Action<string>? ImportError;
    public event Action<string>? ImportStarted;

    public void Dispose()
    {
        _cts.Cancel();
        _watcher.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    private void MoveFileToFolder(string sourcePath, string targetDir, string fileName)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_");
            string destinationPath = Path.Combine(targetDir, timestamp + fileName);
            File.Move(sourcePath, destinationPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Не удалось переместить файл {FileName} в {TargetDir}", fileName, targetDir);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        var reader = _fileQueue.Reader;

        while (await reader.WaitToReadAsync(ct))
        {
            while (reader.TryRead(out var filePath))
            {
                if (ct.IsCancellationRequested) break;
                if (!File.Exists(filePath)) continue;

                await ProcessSingleFileAsync(filePath, ct);
            }
        }
    }

    private async Task ProcessSingleFileAsync(string filePath, CancellationToken ct)
    {
        string fileName = Path.GetFileName(filePath);
        ImportStarted?.Invoke(fileName);
        _logger?.LogInformation("Обнаружен новый файл для автоимпорта: {FileName}", fileName);

        const int maxRetries = 10;
        const int delayMs = 500;
        bool isFileReady = false;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                isFileReady = true;
                break;
            }
            catch (IOException)
            {
                await Task.Delay(delayMs, ct);
            }
        }

        if (!isFileReady)
        {
            string msg = $"Файл {fileName} заблокирован другим процессом.";
            _logger?.LogWarning(msg);
            ImportError?.Invoke(msg);
            MoveFileToFolder(filePath, _errorPath, fileName);
            return;
        }

        try
        {
            int addedCount = await _dataImportService.ImportCsvAsync(filePath, ct);

            if (addedCount > 0)
            {
                DataImported?.Invoke(addedCount);
            }

            MoveFileToFolder(filePath, _archivePath, fileName);
            _logger?.LogInformation("Автоимпорт файла {FileName} успешно завершен ({AddedCount} новых записей).", fileName, addedCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка автоимпорта файла {FileName}", fileName);
            ImportError?.Invoke($"Ошибка автоимпорта файла {fileName}:\n{ex.Message}");
            MoveFileToFolder(filePath, _errorPath, fileName);
        }
    }
}
