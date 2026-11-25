using System.Windows;

namespace QuickPos;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (sender, e) =>
        {
            MessageBox.Show(
                $"Application Error:\n{e.Exception.Message}\n\nLocation:\n{e.Exception.StackTrace}",
                "QuickPOS Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        };
    }
}
