using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;
using System.Windows;

namespace PersonelTakip.Monitoring.ViewModels;

public partial class ParolaDegistirViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly Kullanici _currentUser;

    [ObservableProperty]
    private string _eskiSifre = string.Empty;

    [ObservableProperty]
    private string _yeniSifre = string.Empty;

    [ObservableProperty]
    private string _yeniSifreTekrar = string.Empty;

    public ParolaDegistirViewModel(DatabaseService databaseService, Kullanici currentUser)
    {
        _databaseService = databaseService;
        _currentUser = currentUser;
    }

    [RelayCommand]
    private async Task KaydetAsync()
    {
        if (string.IsNullOrWhiteSpace(EskiSifre) || string.IsNullOrWhiteSpace(YeniSifre))
        {
            ShowError("Tüm alanları doldurunuz.");
            return;
        }

        if (YeniSifre.Length < 4)
        {
            ShowError("Yeni şifre en az 4 karakter olmalıdır.");
            return;
        }

        if (YeniSifre != YeniSifreTekrar)
        {
            ShowError("Yeni şifreler uyuşmuyor.");
            return;
        }

        IsBusy = true;
        try
        {
            await _databaseService.ParolaDegistirAsync(_currentUser.Id, EskiSifre, YeniSifre);
            ShowSuccess("Şifreniz başarıyla güncellendi.");
            
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Iptal()
    {
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
        window?.Close();
    }
}
