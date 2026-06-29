using System.Windows;
using System.Windows.Threading;

namespace ChatSystem.Desktop;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
            Shutdown(1);
        }
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowStartupError(e.Exception);
        e.Handled = true;
        Current.Shutdown(1);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    private static void ShowStartupError(Exception ex)
    {
        MessageBox.Show(
            ex.ToString(),
            "ChatSystem Desktop 启动失败",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
