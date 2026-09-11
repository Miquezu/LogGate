using LogGate.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;

namespace LogGate.Views;

/// <summary>
/// Логика взаимодействия для AiChatWindow.xaml
/// </summary>
public partial class AiChatWindow : Window
{
    public AiChatWindow(AiChatViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        ((INotifyCollectionChanged)viewModel.Messages).CollectionChanged += (s, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Application.Current?.Dispatcher.InvokeAsync(() => ChatScrollViewer.ScrollToEnd());
            }
        };
    }

    private void ChatScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ChatScrollViewer.ScrollToVerticalOffset(ChatScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
