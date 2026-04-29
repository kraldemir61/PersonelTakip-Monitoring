using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Helpers;
using PersonelTakip.Models;
using PersonelTakip.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace PersonelTakip.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly EmailService _emailService;

    [ObservableProperty]
    private string _loginKullaniciAdi = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;



    [ObservableProperty]
    private string _registerKullaniciAdi = string.Empty;

    [ObservableProperty]
    private string _registerRol = "User";

    [ObservableProperty]
    private Guid? _registerSantiyeId;

    [ObservableProperty]
    private ObservableCollection<Santiye> _santiyeList = new();

    [ObservableProperty]
    private string _connectionStatus = "Bağlantı kuruluyor...";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isSlowConnection;

    private readonly DispatcherTimer _connectionTimer;

    public event Action<Kullanici>? LoginSuccessful;
    public event Action? RegistrationSuccessful;

    public LoginViewModel()
    {
        _databaseService = new DatabaseService();
        _emailService = new EmailService();

        var (savedEmail, savedPassword) = CredentialHelper.LoadCredential();
        if (!string.IsNullOrEmpty(savedEmail) && !string.IsNullOrEmpty(savedPassword))
        {
            LoginKullaniciAdi = savedEmail; // Eski Email alanı artık kullanıcı adı olarak kullanılıyor
            Password = savedPassword;
            RememberMe = true;
        }

        _connectionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _connectionTimer.Tick += async (s, e) => await CheckConnectionAsync();
        _connectionTimer.Start();

        AppConfiguration.Instance.ConfigurationChanged += async (s, e) => 
        {
            await Task.Delay(1000); // Veritabanının kendine gelmesi için 1 saniye bekle
            _connectionTimer.Interval = TimeSpan.FromMilliseconds(500);
            _ = CheckConnectionAsync();
            _ = LoadSantiyelerAsync();
        };

        _ = CheckConnectionAsync();
        _ = LoadSantiyelerAsync();
    }

    private async Task LoadSantiyelerAsync()
    {
        try
        {
            var santiyeler = await _databaseService.SantiyeleriGetirAsync();
            Application.Current.Dispatcher.Invoke(() => {
                SantiyeList = new ObservableCollection<Santiye>(santiyeler);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Santiye Yükleme Hatası: {ex.Message}");
        }
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var connected = await _databaseService.CheckConnectionAsync();
            stopwatch.Stop();

            if (!connected)
            {
                IsConnected = false;
                IsSlowConnection = false;
                ConnectionStatus = "Sunucu Korumada (Lütfen Bekleyin)";
                // Bağlantı yoksa kontrolü yavaşlat (60 saniye)
                _connectionTimer.Interval = TimeSpan.FromSeconds(60);
            }
            else
            {
                IsConnected = true;
                IsSlowConnection = stopwatch.ElapsedMilliseconds > 2000;
                ConnectionStatus = "Bağlantı Hazır";
                // Bağlantı sağlandıysa normal hıza dön (10 saniye)
                _connectionTimer.Interval = TimeSpan.FromSeconds(10);
            }
        }
        catch
        {
            IsConnected = false;
            ConnectionStatus = "Bağlantı Hatası";
            _connectionTimer.Interval = TimeSpan.FromSeconds(60);
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(LoginKullaniciAdi) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Kullanıcı adı ve parola gereklidir.");
            return;
        }

        IsBusy = true;
        try
        {
            var kullanici = await _databaseService.KullaniciGirisAsync(LoginKullaniciAdi, Password);
            if (kullanici == null)
            {
                ShowError("Geçersiz kullanıcı adı veya parola.");
                return;
            }

            try
            {
                await _databaseService.AuditLogAsync(kullanici.Id, "kullanicilar", kullanici.Id.ToString(), "Giris", null, null, "Giriş yapıldı");
                
                // Tüm adminlere bildirim gönder: kullanıcı online oldu (Süper admin girişi hariç)
                if (kullanici.Rol != "Admin" && kullanici.KullaniciAdi.ToLower() != "admin")
                {
                    var mesaj = $"{kullanici.KullaniciAdi} ({kullanici.Rol}) oturum açtı.";
                    await _databaseService.BildirimEkleAsync(mesaj, kullanici.Id);
                }
            }
            catch { }

            if (RememberMe)
            {
                CredentialHelper.SaveCredential(LoginKullaniciAdi, Password);
            }
            else
            {
                CredentialHelper.DeleteCredential();
            }

            Application.Current.Properties["Kullanici"] = kullanici;
            LoginSuccessful?.Invoke(kullanici);
        }
        catch (Exception ex)
        {
            ShowError($"Giriş sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(RegisterKullaniciAdi))
        {
            ShowError("Kullanıcı adı gereklidir.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Parola gereklidir.");
            return;
        }

        if (Password.Length < 4)
        {
            ShowError("Parola en az 4 karakter olmalıdır.");
            return;
        }

        if (!RegisterSantiyeId.HasValue)
        {
            ShowError("Şantiye seçimi gereklidir.");
            return;
        }

        IsBusy = true;
        try
        {
            var kullanici = new Kullanici
            {
                KullaniciAdi = RegisterKullaniciAdi.Trim(),
                Rol = RegisterRol,
                SantiyeId = RegisterSantiyeId
            };

            var id = await _databaseService.KullaniciOlusturAsync(kullanici, Password);

            // Tüm adminlere bildirim gönder: yeni kullanıcı kayıt oldu
            try
            {
                var santiyeAdi = SantiyeList?.FirstOrDefault(s => s.Id == RegisterSantiyeId)?.Adi ?? "Bilinmiyor";
                var mesaj = $"Yeni kullanıcı kayıt oldu: {RegisterKullaniciAdi.Trim()} ({santiyeAdi})";
                await _databaseService.BildirimEkleAsync(mesaj, id);
            }
            catch { }

            ShowSuccess("Kayıt başarılı! Şimdi giriş yapabilirsiniz.");

            RegistrationSuccessful?.Invoke();
            TemizleFormu();
        }
        catch (Exception ex)
        {
            ShowError($"Kayıt sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToLogin()
    {
        TemizleFormu();
    }

    [RelayCommand]
    private void OpenDatabaseSettings()
    {
        var win = new PersonelTakip.Views.DatabaseSettingsWindow();
        // Aktif pencereyi owner olarak belirle
        win.Owner = System.Linq.Enumerable.FirstOrDefault(System.Windows.Application.Current.Windows.Cast<System.Windows.Window>(), w => w.IsActive);
        win.ShowDialog();
    }

    private void TemizleFormu()
    {
        LoginKullaniciAdi = string.Empty;
        Password = string.Empty;
        RegisterKullaniciAdi = string.Empty;
        RegisterRol = "User";
        IsBusy = false;
    }
}