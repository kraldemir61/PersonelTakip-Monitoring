using System.Windows;
using System.IO;
using System;

namespace PersonelTakip;

public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            LogException(ex.ExceptionObject as Exception, "App Constructor AppDomain.UnhandledException");
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        this.ShutdownMode = ShutdownMode.OnLastWindowClose;
        // Global hata yakalayıcı
        this.DispatcherUnhandledException += (s, ex) =>
        {
            LogException(ex.Exception, "DispatcherUnhandledException");
            MessageBox.Show($"Beklenmedik bir hata oluştu:\n{ex.Exception.Message}", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            LogException(ex.ExceptionObject as Exception, "AppDomain.UnhandledException");
        };

        try 
        {
            var loginWindow = new Views.LoginWindow();
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            LogException(ex, "OnStartup Exception during window creation");
            MessageBox.Show($"Uygulama başlatılamadı (Pencere oluşturma hatası):\n{ex.Message}\n\nDetaylar crash_log.txt dosyasına kaydedildi.", "Başlatma Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    private void LogException(Exception? ex, string source)
    {
        if (ex == null) return;
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            string message = $"[{DateTime.Now}] Source: {source}\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n{new string('-', 50)}\n";
            File.AppendAllText(logPath, message);
        }
        catch { }
    }
}
