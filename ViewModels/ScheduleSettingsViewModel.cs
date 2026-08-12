using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Interfaces;
using LogGate.Models;
using LogGate.Services;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace LogGate.ViewModels
{
    public partial class ScheduleSettingsViewModel : ObservableObject
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<WorkScheduleRule> _rules = [];

        [ObservableProperty]
        private WorkScheduleRule? _selectedRule;

        [ObservableProperty]
        private string _newTargetName = string.Empty;

        [ObservableProperty]
        private bool _newIsPersonal;

        // По умолчанию ставим 08:00 и 16:30
        [ObservableProperty]
        private DateTime? _newStartTime = new DateTime(2000, 1, 1, 8, 0, 0);

        [ObservableProperty]
        private DateTime? _newEndTime = new DateTime(2000, 1, 1, 16, 30, 0);

        public ScheduleSettingsViewModel(IDataRepository dataRepository, IDialogService dialogService)
        {
            _dataRepository = dataRepository;
            _dialogService = dialogService;
            LoadRules();
        }

        private void LoadRules()
        {
            var dbRules = _dataRepository.GetAllWorkRules();
            Rules = new ObservableCollection<WorkScheduleRule>(dbRules);
        }

        [RelayCommand]
        private void AddRule()
        {
            if (string.IsNullOrWhiteSpace(NewTargetName) || !NewStartTime.HasValue || !NewEndTime.HasValue)
            {
                _dialogService.ShowMessage("Пожалуйста, заполните все поля.");
                return;
            }

            var rule = new WorkScheduleRule
            {
                TargetName = NewTargetName.Trim(),
                IsPersonal = NewIsPersonal,
                StartTime = NewStartTime.Value.TimeOfDay,
                EndTime = NewEndTime.Value.TimeOfDay
            };

            Rules.Add(rule);
            NewTargetName = string.Empty; // Очищаем поле после добавления
        }

        [RelayCommand]
        private void DeleteRule()
        {
            if (SelectedRule != null)
                Rules.Remove(SelectedRule);
        }

        [RelayCommand]
        private void SaveChanges()
        {
            try
            {
                var rulesList = Rules.ToList();
                _dataRepository.SaveWorkRules(rulesList);

                // Мгновенно обновляем правила в движке, не перезапуская приложение
                ScheduleRules.UpdateRules(rulesList, ScheduleRules.PreHolidays);

                _dialogService.ShowMessage("Настройки графиков успешно сохранены!");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка при сохранении: {ex.Message}");
            }
        }
    }
}