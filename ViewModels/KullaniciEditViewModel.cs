using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Models;
using PersonelTakip.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace PersonelTakip.ViewModels;

public partial class KullaniciEditViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly EmailService _emailService;
    private readonly Kullanici _currentUser;
    private readonly Guid? _kullaniciId;
    private readonly string? _orijinalKullaniciAdi;

    [ObservableProperty]
    private string _kullaniciAdi = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _sifre = string.Empty;

    [ObservableProperty]
    private string _sifreTekrar = string.Empty;

    [ObservableProperty]
    private string _rol = "User";

    [ObservableProperty]
    private Guid? _santiyeId;

    [ObservableProperty]
    private ObservableCollection<Santiye> _santiyeList = new();

    [ObservableProperty]
    private string _windowTitle = "Yeni Kullanıcı";

    public bool IsEditMode => _kullaniciId.HasValue;

    public string[] Roller => new[] { "Admin", "User" };

    public KullaniciEditViewModel(DatabaseService databaseService, EmailService emailService, Kullanici currentUser, Kullanici? kullanici = null)
    {
        _databaseService = databaseService;
        _emailService = emailService;
        _currentUser = currentUser;
        _kullaniciId = kullanici?.Id;
        _orijinalKullaniciAdi = kullanici?.KullaniciAdi;

        if (kullanici != null)
        {
            WindowTitle = "Kullanıcı Düzenle";
            KullaniciAdi = kullanici.KullaniciAdi;
            Email = kullanici.Email;
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

        // Eğer düzenlenen kişi orijinalde "Roujin61" ise:
        if (!string.IsNullOrEmpty(_orijinalKullaniciAdi) && 
            _orijinalKullaniciAdi.Equals("Roujin61", StringComparison.OrdinalIgnoreCase))
        {
            // Kullanıcı adı değişmişse izin verme
            if (!KullaniciAdi.Equals("Roujin61", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Süper Admin hesabının (Roujin61) kullanıcı adı değiştirilemez.");
                return;
            }

            // Rolü User yapılmışsa izin verme
            if (Rol != "Admin")
            {
                ShowError("Süper Admin hesabının (Roujin61) yetkisi 'User' olarak düşürülemez.");
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
                Email = Email.Trim(),
                Rol = Rol,
                SantiyeId = SantiyeId
            };

            if (IsEditMode)
            {
                await _databaseService.KullaniciGuncelleAsync(kullanici, _currentUser.Id);
            }
            else
            {
                var yeniId = await _databaseService.KullaniciOlusturAsync(kullanici, sifre);
                await _emailService.KullaniciOlusturmaBildirimiGonderAsync(Email, KullaniciAdi, sifre);
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
    private void Cancel()
    {
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.KullaniciEditWindow);
        window?.Close();
    }
}
