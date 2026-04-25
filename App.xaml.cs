using System.Windows;
using System.IO;
using System;

namespace PersonelTakip;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            LogException(ex, "OnStartup Exception");
            MessageBox.Show($"Uygulama başlatılamadı:\n{ex.Message}", "Başlatma Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
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
