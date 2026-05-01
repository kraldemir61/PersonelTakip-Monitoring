using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;

namespace PersonelTakip.Monitoring.ViewModels
{
    public partial class CihazEditViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly bool _isEdit;
        private readonly Cihaz? _cihaz;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _seriNo = string.Empty;

        [ObservableProperty]
        private string _cihazAdi = string.Empty;

        [ObservableProperty]
        private string _marka = string.Empty;

        [ObservableProperty]
        private string _model = string.Empty;
        
        [ObservableProperty]
        private string _ozellik = string.Empty;

        [ObservableProperty]
        private string _sahipFirma = string.Empty;

        [ObservableProperty]
        private string _not = string.Empty;

        [ObservableProperty]
        private CihazTuru _tur;

        [ObservableProperty]
        private Guid? _santiyeId;

        [ObservableProperty]
        private ObservableCollection<Santiye> _santiyeList = [];

        [ObservableProperty]
        private ObservableCollection<LookupItem> _cihazAdlari = [];
        [ObservableProperty]
        private ObservableCollection<LookupItem> _markalar = [];
        [ObservableProperty]
        private ObservableCollection<LookupItem> _modeller = [];
        [ObservableProperty]
        private ObservableCollection<LookupItem> _firmalar = [];
 
        [ObservableProperty]
        private bool _isSuperAdmin;



        public CihazEditViewModel(DatabaseService databaseService, CihazTuru tur, Cihaz? cihaz = null)
        {
            _databaseService = databaseService;
            Tur = tur;
            _isEdit = cihaz != null;
            _cihaz = cihaz ?? new Cihaz { Id = Guid.NewGuid(), Tur = tur, Durum = "Boşta", SonIslemTarihi = DateTime.Now };

            Title = _isEdit ? "Cihaz Düzenle" : (tur == CihazTuru.Olcum ? "Yeni Ölçüm Cihazı Ekle" : "Yeni Ofis Cihazı Ekle");
            
            if (_isEdit && _cihaz != null)
            {
                SeriNo = _cihaz.SeriNo ?? string.Empty;
                CihazAdi = _cihaz.CihazAdi ?? string.Empty;
                Marka = _cihaz.Marka ?? string.Empty;
                Model = _cihaz.Model ?? string.Empty;
                Ozellik = _cihaz.Ozellik ?? string.Empty;
                SahipFirma = _cihaz.SahipFirma ?? string.Empty;
                Not = _cihaz.Not ?? string.Empty;
                SantiyeId = _cihaz.SantiyeId;

            }

            _ = LoadLookupsAsync();
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                var adlar = await _databaseService.LookupGetirAsync("cihaz_adlari");
                var markalar = await _databaseService.LookupGetirAsync("cihaz_markalari");
                var modeller = await _databaseService.LookupGetirAsync("cihaz_modelleri");
                var firmalar = await _databaseService.LookupGetirAsync("cihaz_firmalari");

                CihazAdlari = new ObservableCollection<LookupItem>(adlar);
                Markalar = new ObservableCollection<LookupItem>(markalar);
                Modeller = new ObservableCollection<LookupItem>(modeller);
                Firmalar = new ObservableCollection<LookupItem>(firmalar);
            }
            catch { /* Hata yönetimi gerekebilir */ }
        }

        [RelayCommand]
        private async Task KaydetAsync(Window window)
        {
            if (string.IsNullOrWhiteSpace(SeriNo) || string.IsNullOrWhiteSpace(CihazAdi))
            {
                MessageBox.Show("Seri No ve Cihaz Adı boş bırakılamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_cihaz == null) return;

            try
            {
                _cihaz.SeriNo = SeriNo;
                _cihaz.CihazAdi = CihazAdi;
                _cihaz.Marka = Marka;
                _cihaz.Model = Model;
                _cihaz.Ozellik = Ozellik;
                _cihaz.SahipFirma = SahipFirma;
                _cihaz.Not = Not;
                _cihaz.SantiyeId = SantiyeId;
                _cihaz.Durum = SantiyeId.HasValue ? "Şantiyede" : (_cihaz.Durum ?? "Boşta");

                if (_isEdit)
                    await _databaseService.CihazGuncelleAsync(_cihaz);
                else
                    await _databaseService.CihazEkleAsync(_cihaz);

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
