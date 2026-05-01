using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;
using System.Windows;

namespace PersonelTakip.Monitoring.ViewModels;

public partial class SantiyeEditViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly Kullanici _currentUser;
    private readonly Guid? _santiyeId;

    [ObservableProperty]
    private string _adi = string.Empty;

    [ObservableProperty]
    private string _kod = string.Empty;

    [ObservableProperty]
    private string _adres = string.Empty;

    [ObservableProperty]
    private string _windowTitle = "Yeni Şantiye";

    public bool IsEditMode => _santiyeId.HasValue;

    public SantiyeEditViewModel(DatabaseService databaseService, Kullanici currentUser, Santiye? santiye = null)
    {
        _databaseService = databaseService;
        _currentUser = currentUser;
        _santiyeId = santiye?.Id;

        if (santiye != null)
        {
            WindowTitle = "Şantiye Düzenle";
            Adi = santiye.Adi;
            Kod = santiye.Kod;
            Adres = santiye.Adres ?? string.Empty;
        }
        else
        {
            WindowTitle = "Yeni Şantiye";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Adi))
        {
            ShowError("Şantiye adı gereklidir.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Kod))
        {
            ShowError("Şantiye kodu gereklidir.");
            return;
        }

        IsBusy = true;
        try
        {
            var santiye = new Santiye
            {
                Id = _santiyeId ?? Guid.Empty,
                Adi = Adi.Trim(),
                Kod = Kod.Trim().ToUpper(),
                Adres = string.IsNullOrWhiteSpace(Adres) ? null : Adres.Trim()
            };

            if (IsEditMode)
            {
                await _databaseService.SantiyeGuncelleAsync(santiye, _currentUser.Id);
            }
            else
            {
                await _databaseService.SantiyeOlusturAsync(santiye);
            }

            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.SantiyeEditWindow);
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
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.SantiyeEditWindow);
        window?.Close();
    }
}
