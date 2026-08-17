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

            // Создаем папку AutoImport рядом с исполняемым файлом (exe)
            string importPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoImport");
            if (!Directory.Exists(importPath))
            {
                Directory.CreateDirectory(importPath);
            }

            // Настраиваем слежку за папкой (ищем только .csv)
            _watcher = new FileSystemWatcher(importPath, "*.csv")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            // Подписываемся на событие создания нового файла
            _watcher.Created += OnFileCreated;
        }

        // События, чтобы сообщать интерфейсу (MainViewModel) об успехах или ошибках
        public event Action<int>? DataImported;

        public event Action<string>? ImportError;

        public void Dispose()
        {
            _watcher?.Dispose();
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            // Используем асинхронность, чтобы не блокировать интерфейс программы
            await ProcessFileAsync(e.FullPath);
        }

        private async Task ProcessFileAsync(string filePath)
        {
            // Ждем 1 секунду, чтобы стороннее приложение успело закончить запись файла
            await Task.Delay(1000);

            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Пробуем парсить и сохранять
                    var parsedData = _fileParser.Parse(filePath);
                    int addedCount = _dataRepository.SaveItems(parsedData);

                    if (addedCount > 0)
                    {
                        // Оповещаем интерфейс, что есть новые данные
                        DataImported?.Invoke(addedCount);
                    }

                    // Опционально: удаляем файл после успешного импорта, чтобы не захламлять папку
                    // File.Delete(filePath);

                    break; // Успешно обработали, вырываемся из цикла
                }
                catch (IOException)
                {
                    // Файл все еще заблокирован другим процессом (еще копируется).
                    // Ждем 2 секунды и пробуем снова.
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    // Попался "битый" файл или другая ошибка
                    ImportError?.Invoke($"Ошибка автоимпорта файла {Path.GetFileName(filePath)}:\n{ex.Message}");
                    break;
                }
            }
        }
    }
}