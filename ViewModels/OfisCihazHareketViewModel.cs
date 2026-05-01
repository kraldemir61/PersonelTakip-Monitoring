using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace PersonelTakip.Monitoring.ViewModels
{
    public partial class OfisCihazHareketViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly Kullanici _currentUser;
        private readonly OfisCihazi _cihaz;

        [ObservableProperty]
        private string _cihazBilgi = string.Empty;

        [ObservableProperty]
        private string _islemTuru = string.Empty;

        [ObservableProperty]
        private Guid? _nereyeSantiyeId;

        [ObservableProperty]
        private string _aciklama = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Santiye> _santiyeList = [];

        public List<string> IslemTurleri { get; } = new List<string>
        {
            "Arızalı",
            "Format Atılacak",
            "Program Yüklenecek",
            "Değişim Olacak",
            "Merkez Depoya İade",
            "Diğer"
        };

        public OfisCihazHareketViewModel(DatabaseService databaseService, Kullanici currentUser, OfisCihazi cihaz)
        {
            _databaseService = databaseService;
            _currentUser = currentUser;
            _cihaz = cihaz;

            CihazBilgi = $"{cihaz.CihazAdi} ({cihaz.SeriNo}) - Şu anki Konum: {cihaz.BulunduguSantiyeKod ?? "Belirsiz"}";
            
            // Varsayılan işlem türü
            IslemTuru = "Diğer";
            
            // Mevcut şantiye ID'sini al (varsayılan olarak)
            NereyeSantiyeId = cihaz.SantiyeId;
        }

        [RelayCommand]
        private async Task KaydetAsync(Window window)
        {
            if (string.IsNullOrWhiteSpace(Aciklama))
            {
                MessageBox.Show("Lütfen bir açıklama giriniz. Açıklama girmek zorunludur.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NereyeSantiyeId == null)
            {
                MessageBox.Show("Lütfen bir hedef konum (şantiye) seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var hareket = new CihazHareket
                {
                    Id = Guid.NewGuid(),
                    CihazId = _cihaz.Id,
                    NeredenSantiyeId = _cihaz.SantiyeId,
                    NereyeSantiyeId = NereyeSantiyeId,
                    Tarih = DateTime.Now,
                    KullaniciId = _currentUser.Id,
                    Aciklama = Aciklama,
                    IslemTuru = IslemTuru,
                    CihazTuru = CihazTuru.Ofis // Ofis
                };

                await _databaseService.CihazHareketKaydetAsync(hareket);
                
                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hareket kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
