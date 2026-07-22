using CommunityToolkit.Mvvm.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Interfaces;
using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace LogGate.ViewModels
{
    public partial class MainViewModel: ObservableObject
    {
        private readonly IFileParser _fileParser;
        private readonly IDataRepository _dataRepository;

        [ObservableProperty]
        private ObservableCollection<DataItem> _dataItems = new();

        public MainViewModel(IFileParser fileParser, IDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
            _fileParser = fileParser;

            LoadDataFromDatabase();
        }

        private void LoadDataFromDatabase()
        {
            var dbData = _dataRepository.GetAllItems();

            DataItems.Clear();
            foreach (var item in dbData)
            {
                DataItems.Add(item);
            }
        }
        // RelayCommand превратит этот метод в команду LoadDataCommand
        [RelayCommand]
        private void LoadData()
        {
            // Путь к файлу (позже можно добавить OpenFileDialog для выбора пользователем)
            string filePath = @"1.csv";

            if (File.Exists(filePath))
            {
                // 1. Парсим данные из файла CSV
                var parsedData = _fileParser.Parse(filePath);

                // 2. Сохраняем эти новые данные в базу SQLite
                _dataRepository.SaveItems(parsedData);

                // 3. Обновляем интерфейс — загружаем все данные прямо из базы
                LoadDataFromDatabase();
            }
        }
    }
}
