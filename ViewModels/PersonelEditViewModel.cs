using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Models;
using PersonelTakip.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace PersonelTakip.ViewModels;

public partial class PersonelEditViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly Kullanici _currentUser;
    private readonly Guid? _personelId;

    [ObservableProperty]
    private string _adiSoyadi = string.Empty;

    [ObservableProperty]
    private Guid? _santiyeId;

    [ObservableProperty]
    private int? _bolumu;

    [ObservableProperty]
    private int? _gorevi;

    [ObservableProperty]
    private int? _uyrugu;

    [ObservableProperty]
    private DateTime? _iseGirisTarihi;

    [ObservableProperty]
    private string _telefonNumarasi = string.Empty;

    [ObservableProperty]
    private decimal? _maas;

    [ObservableProperty]
    private int? _paraBirimi;

    [ObservableProperty]
    private ObservableCollection<LookupItem> _bolumler = new();

    [ObservableProperty]
    private ObservableCollection<LookupItem> _gorevler = new();

    [ObservableProperty]
    private ObservableCollection<LookupItem> _uyruklar = new();

    [ObservableProperty]
    private ObservableCollection<Santiye> _santiyeIdList = new();

    [ObservableProperty]
    private ObservableCollection<LookupItem> _paraBirimleri = new();

    [ObservableProperty]
    private string _windowTitle = "Yeni Personel";

    public bool IsEditMode => _personelId.HasValue;
    public bool IsAdmin { get; }
    public bool IsSuperAdmin { get; }

    public PersonelEditViewModel(DatabaseService databaseService, Kullanici currentUser, bool isAdmin, Personel? personel = null)
    {
        _databaseService = databaseService;
        _currentUser = currentUser;
        _personelId = personel?.Id;
        IsAdmin = isAdmin;
        IsSuperAdmin = IsAdmin && currentUser?.KullaniciAdi?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;

        if (personel != null)
        {
            WindowTitle = "Personel Düzenle";
            AdiSoyadi = personel.AdiSoyadi;
            SantiyeId = personel.SantiyeId;
            Bolumu = personel.Bolumu;
            Gorevi = personel.Gorevi;
            Uyrugu = personel.Uyrugu;
            IseGirisTarihi = personel.IseGirisTarihi;
            TelefonNumarasi = personel.TelefonNumarasi ?? string.Empty;
            Maas = personel.Maas;
            ParaBirimi = personel.ParaBirimi;
        }
        else
        {
            WindowTitle = "Yeni Personel";
            SantiyeId = currentUser.SantiyeId;
            IseGirisTarihi = DateTime.Today;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(AdiSoyadi))
        {
            ShowError("Adı Soyadı gereklidir.");
            return;
        }

        IsBusy = true;
        try
        {
            var personel = new Personel
            {
                Id = _personelId ?? Guid.Empty,
                AdiSoyadi = AdiSoyadi.Trim(),
                SantiyeId = SantiyeId,
                Bolumu = Bolumu,
                Gorevi = Gorevi,
                Uyrugu = Uyrugu,
                IseGirisTarihi = IseGirisTarihi,
                TelefonNumarasi = string.IsNullOrWhiteSpace(TelefonNumarasi) ? null : TelefonNumarasi.Trim(),
                Maas = Maas,
                ParaBirimi = ParaBirimi
            };

            if (_personelId.HasValue)
            {
                await _databaseService.PersonelGuncelleAsync(personel);
            }
            else
            {
                await _databaseService.PersonelOlusturAsync(personel);
            }

            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.PersonelEditWindow);
            if (window != null)
            {
                window.DialogResult = true;
                window.Close();
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
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.PersonelEditWindow);
        window?.Close();
    }
}