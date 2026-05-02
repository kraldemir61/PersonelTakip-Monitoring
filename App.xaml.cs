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
            
            Kullanici? actualUser = null;
            try 
            {
                // Veritabanında bu isimde kullanıcı var mı kontrol et
                actualUser = await db.KullaniciGetirByNameAsync(_monitorName);
                
                if (actualUser == null)
                {
                    // Yoksa oluştur
                    var newUser = new Kullanici { 
                        KullaniciAdi = _monitorName, 
                        Rol = "Monitor",
                        Email = $"{_monitorName}@system.local"
                    };
                    var newId = await db.KullaniciOlusturAsync(newUser, "123456");
                    
                    // Oluşturulan kullanıcıyı ID'si ile birlikte tekrar çek
                    actualUser = await db.KullaniciGetirIdAsync(newId);
                }
                else
                {
                    // Varsa sadece Aktif hale getir
                    await db.KullaniciDurumGuncelleByNameAsync(_monitorName, true);
                }

                if (actualUser != null)
                {
                    Application.Current.Properties["Kullanici"] = actualUser;
                }
                else
                {
                    // Kritik hata: Kullanıcı ne bulundu ne oluşturulabildi
                    throw new Exception("Kullanıcı kimliği doğrulanamadı.");
                }
            }
            catch (Exception ex)
            {
                LogException(ex, "AutoLogin/Registration Error");
                // Fallback (En azından uygulama açılmaya çalışsın ama heartbeat muhtemelen çalışmayacaktır)
                actualUser = new Kullanici 
                { 
                    KullaniciAdi = _monitorName, 
                    Rol = "Monitor",
                    SantiyeAdi = "İzleyici"
                };
                Application.Current.Properties["Kullanici"] = actualUser;
            }

            // Doğrudan ana ekrana geçiyoruz
            var mainWindow = new Views.MainWindow();
            this.MainWindow = mainWindow;
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            // Giriş bildirimi gönder (Admin ekranını anlık tetiklemek için)
            try
            {
                if (actualUser != null && actualUser.Id != Guid.Empty)
                {
                    var mesaj = $"{_monitorName} (İzleyici) oturum açtı.";
                    await db.BildirimEkleAsync(mesaj, actualUser.Id);
                }
            }
            catch { }
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
