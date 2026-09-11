using LogGate.ViewModels;
using MaterialDesignThemes.Wpf;
using System;

namespace LogGate.Interfaces
{
    public interface IDialogService
    {
        ISnackbarMessageQueue SnackbarMessageQueue { get; }

        void OpenAiChat(AiChatViewModel viewModel);

        void OpenEmployeeCard(EmployeeCardViewModel viewModel);

        string? OpenFileDialog();

        string? SaveFileDialog(string defaultFileName, string filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*");

        bool? OpenScheduleSettings(ScheduleSettingsViewModel viewModel);

        void ShowError(string message, string title = "Ошибка");

        void ShowMessage(string message, string title = "Уведомление");

        void ShowWarning(string message, string title = "Внимание");

        void ShowMessageWithAction(string message, string actionText, Action actionHandler);
    }
}