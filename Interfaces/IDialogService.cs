using LogGate.ViewModels;
using MaterialDesignThemes.Wpf;

namespace LogGate.Interfaces;

/// <summary>
/// Сервис управления диалоговыми окнами и уведомлениями интерфейса.
/// </summary>
public interface IDialogService
{
    ISnackbarMessageQueue SnackbarMessageQueue { get; }

    void OpenAiChat(AiChatViewModel viewModel);

    void OpenEmployeeCard(string employeeName);

    bool? OpenScheduleSettings();

    string? OpenFileDialog();

    string? SaveFileDialog(string defaultFileName, string filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*");

    void ShowError(string message, string title = "Ошибка");

    void ShowMessage(string message, string title = "Уведомление");

    void ShowWarning(string message, string title = "Внимание");

    void ShowMessageWithAction(string message, string actionText, Action actionHandler);
}
}