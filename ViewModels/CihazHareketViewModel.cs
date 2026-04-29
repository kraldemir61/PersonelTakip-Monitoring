using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Models;
using PersonelTakip.Services;

namespace PersonelTakip.ViewModels
{
    public partial class CihazHareketViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly Cihaz _cihaz;
        private readonly Kullanici _currentUser;

        [ObservableProperty]
        private string _cihazBilgi;

        [ObservableProperty]
        private Guid? _nereyeSantiyeId;

        [ObservableProperty]
        private ObservableCollection<Santiye> _santiyeList;

        [ObservableProperty]
        private string _islemTuru; // Şantiyeye Sevk, Arıza, Bakım, Kalibrasyon

        [ObservableProperty]
        private ObservableCollection<string> _islemTurleri = new() { "Şantiyeye Sevk", "Arıza", "Bakım", "Kalibrasyon", "Diğer" };

        [ObservableProperty]
        private string _aciklama;

        [ObservableProperty]
        private bool _isSuperAdmin;

        public CihazHareketViewModel(DatabaseService databaseService, Kullanici currentUser, Cihaz cihaz)
        {
            _databaseService = databaseService;
            _currentUser = currentUser;
            _cihaz = cihaz;

            CihazBilgi = $"{cihaz.SeriNo} - {cihaz.CihazAdi} ({cihaz.Marka} {cihaz.Model})";
            IslemTuru = "Şantiyeye Sevk";
            NereyeSantiyeId = cihaz.SantiyeId;
        }

        [RelayCommand]
        private async Task KaydetAsync(Window window)
        {
            if (IslemTuru == "Diğer" && string.IsNullOrWhiteSpace(Aciklama))
            {
                MessageBox.Show("'Diğer' işlem türü seçildiğinde açıklama yazılması zorunludur.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Güvenlik Kontrolü: Süper admin değilse
            if (!IsSuperAdmin)
            {
                // Boştaki cihazı çekiyorsa, sadece kendi şantiyesine çekebilir
                if (_cihaz.SantiyeId == null)
                {
                    if (NereyeSantiyeId != _currentUser.SantiyeId)
                    {
                        MessageBox.Show("Boştaki bir cihazı sadece kendi şantiyenize çekebilirsiniz.");
                        return;
                    }
                    IslemTuru = "Şantiyeye Sevk";
                }
            }

            var hareket = new CihazHareket
            {
                Id = Guid.NewGuid(),
                CihazId = _cihaz.Id,
                NeredenSantiyeId = _cihaz.SantiyeId,
                NereyeSantiyeId = NereyeSantiyeId,
                Tarih = DateTime.Now,
                KullaniciId = _currentUser.Id,
                Aciklama = Aciklama,
                IslemTuru = IslemTuru
            };

            try
            {
                await _databaseService.CihazHareketKaydetAsync(hareket);
                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hareket kaydedilirken hata oluştu: " + ex.Message);
            }
        }

        [RelayCommand]
        private void Iptal(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
