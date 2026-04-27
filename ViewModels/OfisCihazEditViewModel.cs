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
        private ObservableCollection<string> _fotograflar = new();

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

                if (!string.IsNullOrEmpty(_cihaz.FotoPath))
                {
                    var paths = _cihaz.FotoPath.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in paths) Fotograflar.Add(p);
                }
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
            }
            catch { }
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
                List<string> savedPaths = new List<string>();
                foreach (var path in Fotograflar)
                {
                    var dest = HandleFotoFile(path);
                    if (!string.IsNullOrEmpty(dest)) savedPaths.Add(dest);
                }

                _cihaz.SeriNo = SeriNo;
                _cihaz.CihazAdi = CihazAdi;
                _cihaz.Marka = Marka;
                _cihaz.Model = Model;
                _cihaz.Ozellik = Ozellik;
                _cihaz.Not = Not;
                _cihaz.FotoPath = string.Join("|", savedPaths);

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
        private void SelectFoto()
        {
            if (Fotograflar.Count >= 3)
            {
                MessageBox.Show("En fazla 3 fotoğraf ekleyebilirsiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var op = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Fotoğraf Seç",
                Filter = "Görsel Dosyaları|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };

            if (op.ShowDialog() == true)
            {
                foreach (var file in op.FileNames)
                {
                    if (Fotograflar.Count < 3) Fotograflar.Add(file);
                }
            }
        }

        [RelayCommand]
        private void RemoveFoto(string path)
        {
            if (Fotograflar.Contains(path)) Fotograflar.Remove(path);
        }

        private string? HandleFotoFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!System.IO.Path.IsPathRooted(path)) return path;

            try
            {
                string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Photos");
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                string ext = System.IO.Path.GetExtension(path);
                string newFileName = $"Cihaz_{Guid.NewGuid()}{ext}";
                string destPath = System.IO.Path.Combine(folder, newFileName);

                System.IO.File.Copy(path, destPath, true);
                return newFileName;
            }
            catch { return null; }
        }

        [RelayCommand]
        private void Iptal(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
