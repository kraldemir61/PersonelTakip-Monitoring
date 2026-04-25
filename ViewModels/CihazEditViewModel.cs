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
    public partial class CihazEditViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly bool _isEdit;
        private readonly Cihaz _cihaz;

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
        private string _sahipFirma;

        [ObservableProperty]
        private string _not;

        [ObservableProperty]
        private CihazTuru _tur;

        [ObservableProperty]
        private Guid? _santiyeId;

        [ObservableProperty]
        private ObservableCollection<Santiye> _santiyeList = new();

        [ObservableProperty]
        private ObservableCollection<LookupItem> _cihazAdlari = new();
        [ObservableProperty]
        private ObservableCollection<LookupItem> _markalar = new();
        [ObservableProperty]
        private ObservableCollection<LookupItem> _modeller = new();
        [ObservableProperty]
        private ObservableCollection<LookupItem> _firmalar = new();
 
        [ObservableProperty]
        private bool _isSuperAdmin;

        public CihazEditViewModel(DatabaseService databaseService, CihazTuru tur, Cihaz? cihaz = null)
        {
            _databaseService = databaseService;
            Tur = tur;
            _isEdit = cihaz != null;
            _cihaz = cihaz ?? new Cihaz { Id = Guid.NewGuid(), Tur = tur, Durum = "Boşta", SonIslemTarihi = DateTime.Now };

            Title = _isEdit ? "Cihaz Düzenle" : (tur == CihazTuru.Olcum ? "Yeni Ölçüm Cihazı Ekle" : "Yeni Ofis Cihazı Ekle");
            
            if (_isEdit)
            {
                SeriNo = _cihaz.SeriNo;
                CihazAdi = _cihaz.CihazAdi;
                Marka = _cihaz.Marka;
                Model = _cihaz.Model;
                SahipFirma = _cihaz.SahipFirma;
                Not = _cihaz.Not;
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

            _cihaz.SeriNo = SeriNo;
            _cihaz.CihazAdi = CihazAdi;
            _cihaz.Marka = Marka;
            _cihaz.Model = Model;
            _cihaz.SahipFirma = SahipFirma;
            _cihaz.Not = Not;
            _cihaz.SantiyeId = SantiyeId;
            _cihaz.Durum = SantiyeId.HasValue ? "Şantiyede" : "Boşta";

            try
            {
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
