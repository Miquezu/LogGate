namespace LogGate.Interfaces
{
    public interface IDialogService
    {
        string? OpenFileDialog();
        void ShowMessage(string message);
    }
}
