using LogGate.ViewModels;

namespace LogGate.Interfaces
{
    public interface IDialogService
    {
        void OpenAiChat(AiChatViewModel viewModel);

        void OpenEmployeeCard(EmployeeCardViewModel viewModel);

        string? OpenFileDialog();

        bool? OpenScheduleSettings(ScheduleSettingsViewModel viewModel);

        void ShowError(string message, string title = "Ошибка");

        void ShowMessage(string message, string title = "Уведомление");

        void ShowWarning(string message, string title = "Внимание");
    }
}