using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PersonelTakip.Monitoring.ViewModels;

public partial class BildirimMerkeziViewModel : ObservableObject
{
    private readonly LocalNotificationService _localService;
    private readonly DatabaseService _databaseService;
    private readonly Guid _kullaniciId;

    [ObservableProperty]
    private ObservableCollection<Bildirim> _bildirimler = new();

    [ObservableProperty]
    private bool _isBusy;

    public BildirimMerkeziViewModel(DatabaseService databaseService, LocalNotificationService localService, Guid kullaniciId)
    {
        _databaseService = databaseService;
        _localService = localService;
        _kullaniciId = kullaniciId;
        _ = LoadBildirimlerAsync();
    }

    [RelayCommand]
    public async Task LoadBildirimlerAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _databaseService.KullaniciGetirIdAsync(_kullaniciId);
            bool isAdmin = string.Equals(user?.Rol, "Admin", StringComparison.OrdinalIgnoreCase) || 
                           string.Equals(user?.Rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
            Guid? userSantiyeId = user?.SantiyeId;

            var list = await _localService.GetLocalBildirimlerAsync();
            
            // UI SEVİYESİNDE SON KİLİT:
            var filtrelenmiş = list.Where(b => 
            {
                var lowerMsg = b.Mesaj.ToLower();
                bool isLogin = lowerMsg.Contains("oturum açtı") || lowerMsg.Contains("giriş yaptı");
                bool isTransfer = lowerMsg.Contains("sevk") || lowerMsg.Contains("onay") || lowerMsg.Contains("red");

                // Adminler her şeyi görebilir (Kendi girişi hariç)
                if (isAdmin)
                {
                    if (isLogin && b.TetikleyenKullaniciId == _kullaniciId) return false;
                    return true;
                }

                // İzleyici/User ise:
                if (isLogin) return false; // Giriş bildirimlerini ASLA görmezler

                // Transfer bildirimleri: Her zaman görsünler
                if (isTransfer) return true;

                // Hedef Şantiye Kontrolü
                Guid target = b.HedefSantiyeId ?? Guid.Empty;
                if (target == userSantiyeId) return true;

                return false;
            }).ToList();

            Bildirimler = new ObservableCollection<Bildirim>(filtrelenmiş);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BildirimSilAsync(Bildirim bildirim)
    {
        if (bildirim == null) return;
        try
        {
            await _databaseService.BildirimSilAsync(bildirim.Id, _kullaniciId);
            await _localService.DeleteBildirimAsync(bildirim.Id);
            Bildirimler.Remove(bildirim);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TumunuSilAsync()
    {
        if (Bildirimler.Count == 0) return;
        if (MessageBox.Show("Tüm bildirimler silinecek. Onaylıyor musunuz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            await _databaseService.TumBildirimleriSilAsync(_kullaniciId);
            await _localService.ClearAllAsync();
            Bildirimler.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OkunduİsaretleAsync(Bildirim bildirim)
    {
        if (bildirim == null || bildirim.OkunduMu) return;
        try
        {
            await _databaseService.OkunmadiIseOkunduYapAsync(bildirim.Id, _kullaniciId);
            await _localService.MarkAsReadAsync(bildirim.Id);
            bildirim.OkunduMu = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TumunuOkunduYapAsync()
    {
        if (Bildirimler.Count == 0 || !Bildirimler.Any(x => !x.OkunduMu)) return;

        try
        {
            await _databaseService.TumunuOkunduYapAsync(_kullaniciId);
            await _localService.MarkAllAsReadAsync();
            
            foreach (var bildirim in Bildirimler)
            {
                bildirim.OkunduMu = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Close(Window window)
    {
        window?.Close();
    }
}
