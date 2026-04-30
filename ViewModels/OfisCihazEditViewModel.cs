using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Models;
using PersonelTakip.Services;

namespace PersonelTakip.ViewModels
{
    public partial class OfisCihazEditViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly bool _isEdit;
        private readonly OfisCihazi _cihaz;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _seriNo;

        [ObservableProperty]
        private string _cihazAdi;

        [ObservableProperty]
        private string _marka;

        [ObservableProperty]
        private string _model;

        [ObservableProperty]
        private string _ozellik;

        [ObservableProperty]
        private string _not;

        [ObservableProperty]
        private ObservableCollection<LookupItem> _cihazAdlari = new();
        [ObservableProperty]
        private ObservableCollection<LookupItem> _markalar = new();
        [ObservableProperty]
        private ObservableCollection<LookupItem> _modeller = new();

        [ObservableProperty]
        private Guid? _santiyeId;

        [ObservableProperty]
        private ObservableCollection<Santiye> _santiyeList;



        public OfisCihazEditViewModel(DatabaseService databaseService, OfisCihazi? cihaz = null)
        {
            _databaseService = databaseService;
            _isEdit = cihaz != null;
            _cihaz = cihaz ?? new OfisCihazi { Id = Guid.NewGuid(), Durum = "Boşta", SonIslemTarihi = DateTime.Now };

            Title = _isEdit ? "Ofis Cihazı Düzenle" : "Yeni Ofis Cihazı Ekle";
            
            if (_isEdit)
            {
                SeriNo = _cihaz.SeriNo;
                CihazAdi = _cihaz.CihazAdi;
                Marka = _cihaz.Marka;
                Model = _cihaz.Model;
                Ozellik = _cihaz.Ozellik;
                Not = _cihaz.Not;
                SantiyeId = _cihaz.SantiyeId;

            }

            _ = LoadLookupsAsync();
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                CihazAdlari = new ObservableCollection<LookupItem>(await _databaseService.LookupGetirAsync("cihaz_adlari"));
                Markalar = new ObservableCollection<LookupItem>(await _databaseService.LookupGetirAsync("cihaz_markalari"));
                Modeller = new ObservableCollection<LookupItem>(await _databaseService.LookupGetirAsync("cihaz_modelleri"));
                
                var santiyeler = await _databaseService.SantiyeleriGetirAsync();
                SantiyeList = new ObservableCollection<Santiye>(santiyeler);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Şantiye yükleme hatası: " + ex.Message);
            }
        }

        [RelayCommand]
        private async Task KaydetAsync(Window window)
        {
            if (string.IsNullOrWhiteSpace(SeriNo) || string.IsNullOrWhiteSpace(CihazAdi))
            {
                MessageBox.Show("Seri No ve Cihaz Adı boş bırakılamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _cihaz.SeriNo = SeriNo;
                _cihaz.CihazAdi = CihazAdi;
                _cihaz.Marka = Marka;
                _cihaz.Model = Model;
                _cihaz.Ozellik = Ozellik;
                _cihaz.Not = Not;
                _cihaz.SantiyeId = SantiyeId;

                if (_isEdit)
                    await _databaseService.OfisCihaziGuncelleAsync(_cihaz);
                else
                    await _databaseService.OfisCihaziEkleAsync(_cihaz);

                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kaydedilirken bir hata oluştu: " + ex.Message);
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
