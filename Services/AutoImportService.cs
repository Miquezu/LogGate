using LogGate.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LogGate.Services
{
    public class AutoImportService : IDisposable
    {
        private readonly IDataCleaningService _cleaningService;
        private readonly IDataRepository _dataRepository;
        private readonly IFileParser _fileParser;
        private readonly FileSystemWatcher _watcher;

        public AutoImportService(IFileParser fileParser, IDataCleaningService cleaningService, IDataRepository dataRepository)
        {
            _fileParser = fileParser;
            _cleaningService = cleaningService;
            _dataRepository = dataRepository;

            string importPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoImport");
            if (!Directory.Exists(importPath))
                Directory.CreateDirectory(importPath);

            string[] existingFiles = Directory.GetFiles(importPath, "*.csv");
            foreach (var file in existingFiles)
            {
                _ = ProcessFileAsync(file);
            }

            _watcher = new FileSystemWatcher(importPath, "*.csv")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
        }

        public event Action<int>? DataImported;

        public event Action<string>? ImportError;

        public event Action<string>? ImportStarted;

        public void Dispose() => _watcher?.Dispose();

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                await ProcessFileAsync(e.FullPath);
            }
            catch (Exception ex)
            {
                ImportError?.Invoke($"Критический сбой в модуле автоимпорта:\n{ex.Message}");
            }
        }

        private async Task ProcessFileAsync(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            ImportStarted?.Invoke(fileName);

            int maxRetries = 10;
            int delayMs = 500;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        stream.Close();
                    }

                    var rawData = _fileParser.Parse(filePath);
                    var cleanedData = _cleaningService.CleanAnomalies(rawData);

                    int addedCount = await _dataRepository.SaveItemsAsync(cleanedData);

                    if (addedCount > 0)
                        DataImported?.Invoke(addedCount);

                    File.Delete(filePath);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    ImportError?.Invoke($"Ошибка автоимпорта файла {fileName}:\n{ex.Message}");
                    break;
                }
            }
        }
    }
}