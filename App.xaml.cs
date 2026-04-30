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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // ÖNEMLİ: Lisans penceresi kapandığında uygulamanın tamamen kapanmaması için 
        // ShutdownMode'u geçici olarak OnExplicitShutdown yapıyoruz.
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Global hata yakalayıcı
        this.DispatcherUnhandledException += (s, ex) =>
        {
            LogException(ex.Exception, "DispatcherUnhandledException");
            MessageBox.Show($"Beklenmedik bir hata oluştu:\n{ex.Exception.Message}", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        try 
        {
            // Veritabanı ön yükleme (Tablo kontrolü)
            var db = new Services.DatabaseService();
            _ = db.InitializeDatabaseAsync();

            // LİSANS KONTROLÜ: Eğer program zaten lisanslıysa pencereyi hiç açma
            bool isLicensed = await Task.Run(async () => await Services.LicenseManager.IsLicensedAsync());
            if (!isLicensed)
            {
                // Lisans Ekranı (Lisanssızsa veya Demo modundaysa her çalıştırmada açılsın istendi)
                var licenseWindow = new Views.LicenseWindow();
                bool? result = licenseWindow.ShowDialog();
                
                // Eğer DialogResult=true değilse (Kapat'a basılmışsa veya X ile kapatılmışsa)
                // Yine de arka planda geçerli lisans/demo var mı bakıyoruz.
                if (result != true)
                {
                    var demoInfo = await Task.Run(async () => await Services.LicenseManager.GetDemoSummaryAsync());
                    
                    if (demoInfo.Status != Services.LicenseManager.DemoStatus.Active)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
            }

            // Eğer veritabanı ayarları henüz yapılmamışsa (ilk kurulum), önce ayarlar penceresini aç
            if (string.IsNullOrWhiteSpace(Services.AppConfiguration.Instance.Database.Host))
            {
                var settingsWindow = new Views.DatabaseSettingsWindow();
                settingsWindow.ShowDialog();
            }

            // Giriş ekranına geçiyoruz
            var loginWindow = new Views.LoginWindow();
            this.MainWindow = loginWindow;
            this.ShutdownMode = ShutdownMode.OnMainWindowClose; // Artık login/main kapanınca uygulama kapansın
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            LogException(ex, "OnStartup Exception");
            MessageBox.Show($"Uygulama başlatılamadı:\n{ex.Message}", "Başlatma Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
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
