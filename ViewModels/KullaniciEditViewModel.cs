using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace PersonelTakip.Monitoring.ViewModels;

public partial class KullaniciEditViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly Kullanici _currentUser;
    private readonly Guid? _kullaniciId;
    private readonly string? _orijinalKullaniciAdi;
    private readonly string? _orijinalRol;

    [ObservableProperty]
    private string _kullaniciAdi = string.Empty;


    [ObservableProperty]
    private string _sifre = string.Empty;

    [ObservableProperty]
    private string _sifreTekrar = string.Empty;

    [ObservableProperty]
    private string _rol = "User";

    [ObservableProperty]
    private Guid? _santiyeId;

    [ObservableProperty]
    private ObservableCollection<Santiye> _santiyeList = [];

    [ObservableProperty]
    private string _windowTitle = "Yeni Kullanıcı";

    public bool IsEditMode => _kullaniciId.HasValue;

    public string[] Roller => ["Admin", "User"];

    public KullaniciEditViewModel(DatabaseService databaseService, Kullanici currentUser, Kullanici? kullanici = null)
    {
        _databaseService = databaseService;
        _currentUser = currentUser;
        _kullaniciId = kullanici?.Id;
        _orijinalKullaniciAdi = kullanici?.KullaniciAdi;
        _orijinalRol = kullanici?.Rol;

        if (kullanici != null)
        {
            WindowTitle = "Kullanıcı Düzenle";
            KullaniciAdi = kullanici.KullaniciAdi;
            Rol = kullanici.Rol;
            SantiyeId = kullanici.SantiyeId;
        }
        else
        {
            WindowTitle = "Yeni Kullanıcı";
            Rol = "User";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(KullaniciAdi))
        {
            ShowError("Kullanıcı adı gereklidir.");
            return;
        }

        // Eğer düzenlenen kişi orijinalde "Admin" ise:
        if (!string.IsNullOrEmpty(_orijinalKullaniciAdi) && 
            _orijinalKullaniciAdi.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            // Kullanıcı adı değişmişse izin verme
            if (!KullaniciAdi.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Süper Admin hesabının (Admin) kullanıcı adı değiştirilemez.");
                return;
            }

            // Rolü User yapılmışsa izin verme
            if (Rol != "Admin")
            {
                ShowError("Süper Admin hesabının (Admin) yetkisi 'User' olarak düşürülemez.");
                return;
            }
        }


        var sifre = Sifre;
        var sifreTekrar = SifreTekrar;

        var window = Application.Current.Windows.OfType<Views.KullaniciEditWindow>().FirstOrDefault();
        if (window != null)
        {
            sifre = window.Sifre;
            sifreTekrar = window.SifreTekrar;
        }

        if (!IsEditMode)
        {
            if (string.IsNullOrWhiteSpace(sifre) || sifre.Length < 4)
            {
                ShowError("Parola en az 4 karakter olmalıdır.");
                return;
            }

            if (sifre != sifreTekrar)
            {
                ShowError("Parola tekrarı eşleşmiyor.");
                return;
            }
        }

        IsBusy = true;
        try
        {
            var kullanici = new Kullanici
            {
                Id = _kullaniciId ?? Guid.Empty,
                KullaniciAdi = KullaniciAdi.Trim(),
                Email = string.Empty,
                Rol = Rol,
                SantiyeId = SantiyeId
            };

            if (IsEditMode)
            {
                await _databaseService.KullaniciGuncelleAsync(kullanici, _currentUser.Id);
                
                // Eğer rol değişmişse özel bir tetikleme gönderiyoruz
                if (_orijinalRol != Rol && _kullaniciId.HasValue)
                {
                    // Otomatik kapatma sinyali
                    await _databaseService.SistemBildirimiGonderAsync($"RESTART_TARGET:{_kullaniciId.Value}");
                    
                    // Sisteme kalıcı bildirim ekle
                    var yetkiDurumu = Rol == "Admin" ? "Admin yetkisi verildi" : "User (Kullanıcı) yetkisine düşürüldü";
                    var bildirimMesaji = $"{KullaniciAdi} kişisine Sistem Yöneticisi tarafından {yetkiDurumu}.";
                    await _databaseService.BildirimEkleAsync(bildirimMesaji, _currentUser.Id);
                }
            }
            else
            {
                await _databaseService.KullaniciOlusturAsync(kullanici, sifre);
            }

            var editWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.KullaniciEditWindow);
            if (editWindow != null)
            {
                editWindow.DialogResult = true;
                editWindow.Close();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Kaydetme sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static void Cancel()
    {
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.KullaniciEditWindow);
        window?.Close();
    }
}
