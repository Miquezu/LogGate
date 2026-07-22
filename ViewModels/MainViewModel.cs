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

        [ObservableProperty]
        private ObservableCollection<DataItem> _dataItems = new();

        public MainViewModel(IFileParser fileParser)
        {
            _fileParser = fileParser;
        }

        // RelayCommand превратит этот метод в команду LoadDataCommand
        [RelayCommand]
        private void LoadData()
        {
            // Укажите здесь реальный путь к вашему файлу для теста!
            string filePath = @"1.csv";

            if (File.Exists(filePath))
            {
                var parsedData = _fileParser.Parse(filePath);

                // Очищаем старые данные и добавляем новые
                DataItems.Clear();
                foreach (var item in parsedData)
                {
                    DataItems.Add(item);
                }
            }
        }
    }
}
