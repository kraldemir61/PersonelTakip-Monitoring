using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Models;
using PersonelTakip.Services;
using System.Windows;

namespace PersonelTakip.ViewModels;

public partial class LookupEditViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly string _tabloAdi;
    private readonly int? _id;

    [ObservableProperty]
    private string _adi = string.Empty;

    [ObservableProperty]
    private string _windowTitle = "Yeni";

    [ObservableProperty]
    private string _lookupAdiLabel = "Adı:";

    public bool IsEditMode => _id.HasValue;

    public LookupEditViewModel(DatabaseService databaseService, string tabloAdi, string baslik, LookupItem? item = null)
    {
        _databaseService = databaseService;
        _tabloAdi = tabloAdi;
        _id = item?.Id;

        WindowTitle = item == null ? $"Yeni {baslik}" : $"{baslik} Düzenle";
        LookupAdiLabel = $"{baslik} Adı:";
        Adi = item?.Adi ?? string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Adi))
        {
            ShowError("Adı gereklidir.");
            return;
        }

        IsBusy = true;
        try
        {
            if (IsEditMode && _id.HasValue)
            {
                await _databaseService.LookupGuncelleAsync(_tabloAdi, _id.Value, Adi.Trim());
            }
            else
            {
                await _databaseService.LookupOlusturAsync(_tabloAdi, Adi.Trim());
            }

            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.LookupEditWindow);
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
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is Views.LookupEditWindow);
        window?.Close();
    }
}