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
        private string _islemTuru; // Transfer, Arıza, Bakım, Kalibrasyon

        [ObservableProperty]
        private ObservableCollection<string> _islemTurleri = new() { "Transfer", "Arıza", "Bakım", "Kalibrasyon", "Şantiyeye Sevk" };

        [ObservableProperty]
        private string _aciklama;

        public CihazHareketViewModel(DatabaseService databaseService, Kullanici currentUser, Cihaz cihaz)
        {
            _databaseService = databaseService;
            _currentUser = currentUser;
            _cihaz = cihaz;

            CihazBilgi = $"{cihaz.SeriNo} - {cihaz.CihazAdi} ({cihaz.Marka} {cihaz.Model})";
            IslemTuru = "Transfer";
            NereyeSantiyeId = cihaz.SantiyeId;
        }

        [RelayCommand]
        private async Task KaydetAsync(Window window)
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
