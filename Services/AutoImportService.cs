using LogGate.Interfaces;
using System.IO;

namespace LogGate.Services
{
    public class AutoImportService : IDisposable
    {
        private readonly IDataRepository _dataRepository;
        private readonly IFileParser _fileParser;
        private readonly FileSystemWatcher _watcher;

        public AutoImportService(IFileParser fileParser, IDataRepository dataRepository)
        {
            _fileParser = fileParser;
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

        // Добавляем новое событие для старта
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

            await Task.Delay(1000);

            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var parsedData = _fileParser.Parse(filePath);
                    int addedCount = _dataRepository.SaveItems(parsedData);

                    if (addedCount > 0)
                        DataImported?.Invoke(addedCount);

                    File.Delete(filePath);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(2000);
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