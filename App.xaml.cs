using System.Windows;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using PersonelTakip.Monitoring.Models;

namespace PersonelTakip.Monitoring;

public partial class App : Application
{
    private string? _monitorName;
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
            await db.InitializeDatabaseAsync();

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

            // İzleyici Modu: Giriş ekranı olmadan doğrudan başlat
            var machineName = Environment.MachineName;
            _monitorName = $"Izleyici_{machineName}";
            
            try 
            {
                // Veritabanında bu isimde kullanıcı var mı kontrol et
                var allMonitors = await db.KullanicilariGetirAsync(rol: "Monitor", hepsiniGetir: true);
                var existing = allMonitors.FirstOrDefault(u => u.KullaniciAdi.Equals(_monitorName, StringComparison.OrdinalIgnoreCase));
                
                if (existing == null)
                {
                    // Yoksa oluştur (Varsayılan olarak aktif=true olur)
                    var newUser = new Kullanici { 
                        KullaniciAdi = _monitorName, 
                        Rol = "Monitor",
                        Email = $"{_monitorName}@system.local"
                    };
                    await db.KullaniciOlusturAsync(newUser, "123456");
                }
                else
                {
                    // Varsa sadece Aktif hale getir
                    await db.KullaniciDurumGuncelleByNameAsync(_monitorName, true);
                }
            }
            catch (Exception ex)
            {
                LogException(ex, "AutoLogin/Registration Error");
            }

            Application.Current.Properties["Kullanici"] = new Kullanici 
            { 
                KullaniciAdi = _monitorName, 
                Rol = "Monitor",
                SantiyeAdi = "İzleyici"
            };

            // Doğrudan ana ekrana geçiyoruz
            var mainWindow = new Views.MainWindow();
            this.MainWindow = mainWindow;
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            LogException(ex, "OnStartup Exception");
            MessageBox.Show($"Uygulama başlatılamadı:\n{ex.Message}", "Başlatma Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (!string.IsNullOrEmpty(_monitorName))
        {
            try
            {
                var db = new Services.DatabaseService();
                await db.KullaniciTamamenSilByNameAsync(_monitorName);
            }
            catch { /* Sessizce çık */ }
        }
        base.OnExit(e);
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
