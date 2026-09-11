using LogGate.ViewModels;
using LogGate.Views;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Windows;

namespace LogGate.Services;

/// <summary>
/// Реализация сервиса диалогов с внедрением IServiceProvider для фабричного создания окон.
/// </summary>
public class OpenDialog(IServiceProvider services) : IDialogService
{
    public ISnackbarMessageQueue SnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3.5));

    public void OpenAiChat(AiChatViewModel viewModel)
    {
        var window = new AiChatWindow(viewModel);
        SetOwner(window);
        window.Show();
    }

    public void OpenEmployeeCard(string employeeName)
    {
        var viewModel = ActivatorUtilities.CreateInstance<EmployeeCardViewModel>(services, employeeName);
        var window = new EmployeeCardWindow(viewModel);
        SetOwner(window);
        window.Show();
    }

    public bool? OpenScheduleSettings()
    {
        var viewModel = ActivatorUtilities.CreateInstance<ScheduleSettingsViewModel>(services);
        var window = new ScheduleSettingsWindow(viewModel);
        SetOwner(window);
        return window.ShowDialog();
    }

    public string? OpenFileDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите .csv файл",
            Filter = "CSV files(*.csv)|*.csv|All files(*.*)|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFileDialog(string defaultFileName, string filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*")
    {
        var dialog = new SaveFileDialog
        {
            Title = "Экспорт данных",
            FileName = defaultFileName,
            Filter = filter,
            DefaultExt = ".csv",
            AddExtension = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowError(string message, string title = "Ошибка") =>
        SafeEnqueue(() => SnackbarMessageQueue.Enqueue(message));

    public void ShowMessage(string message, string title = "Уведомление") =>
        SafeEnqueue(() => SnackbarMessageQueue.Enqueue(message));

    public void ShowWarning(string message, string title = "Внимание") =>
        SafeEnqueue(() => SnackbarMessageQueue.Enqueue(message));

    public void ShowMessageWithAction(string message, string actionText, Action actionHandler) =>
        SafeEnqueue(() => SnackbarMessageQueue.Enqueue(message, actionText, actionHandler));

    private static void SafeEnqueue(Action action)
    {
        if (Application.Current?.Dispatcher is null || Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Application.Current.Dispatcher.BeginInvoke(action);
        }
    }

    private static void SetOwner(Window window)
    {
        if (Application.Current?.MainWindow is { } mainWindow && mainWindow != window)
        {
            window.Owner = mainWindow;
        }
    }
}
}