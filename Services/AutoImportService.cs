using LogGate.Interfaces;
using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LogGate.Services
{
    public class AutoImportService : IDisposable
    {
        private readonly string _archivePath;
        private readonly IDataCleaningService _cleaningService;
        private readonly CancellationTokenSource _cts = new();
        private readonly IDataRepository _dataRepository;
        private readonly string _errorPath;
        private readonly IFileParser _fileParser;

        private readonly Channel<string> _fileQueue = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true });

        private readonly string _importPath;
        private readonly FileSystemWatcher _watcher;

        public AutoImportService(IFileParser fileParser, IDataCleaningService cleaningService, IDataRepository dataRepository)
        {
            _fileParser = fileParser;
            _cleaningService = cleaningService;
            _dataRepository = dataRepository;

            _importPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoImport");
            _archivePath = Path.Combine(_importPath, "Archive");
            _errorPath = Path.Combine(_importPath, "Errors");

            Directory.CreateDirectory(_importPath);
            Directory.CreateDirectory(_archivePath);
            Directory.CreateDirectory(_errorPath);

            // Запуск последовательного обработчика очереди
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
            _watcher?.Dispose();
            _cts.Dispose();
        }

        private static void MoveFileToFolder(string sourcePath, string targetDir, string fileName)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_");
                string destinationPath = Path.Combine(targetDir, timestamp + fileName);
                File.Move(sourcePath, destinationPath, overwrite: true);
            }
            catch
            {
                // Исключение повторных сбоев при ошибке файловой системы
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

                    await ProcessSingleFileAsync(filePath);
                }
            }
        }

        private async Task ProcessSingleFileAsync(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            ImportStarted?.Invoke(fileName);

            const int maxRetries = 10;
            const int delayMs = 500;
            bool isFileReady = false;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        isFileReady = true;
                        break;
                    }
                }
                catch (IOException)
                {
                    await Task.Delay(delayMs);
                }
            }

            if (!isFileReady)
            {
                ImportError?.Invoke($"Файл {fileName} заблокирован другим процессом.");
                MoveFileToFolder(filePath, _errorPath, fileName);
                return;
            }

            try
            {
                var rawData = _fileParser.Parse(filePath);
                var cleanedData = _cleaningService.CleanAnomalies(rawData);

                int addedCount = await _dataRepository.SaveItemsAsync(cleanedData);

                if (addedCount > 0)
                    DataImported?.Invoke(addedCount);

                MoveFileToFolder(filePath, _archivePath, fileName);
            }
            catch (Exception ex)
            {
                ImportError?.Invoke($"Ошибка автоимпорта файла {fileName}:\n{ex.Message}");
                MoveFileToFolder(filePath, _errorPath, fileName);
            }
        }
    }
}