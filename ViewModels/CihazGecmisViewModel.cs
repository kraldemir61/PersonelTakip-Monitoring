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
    public partial class CihazGecmisViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly Cihaz _cihaz;

        [ObservableProperty]
        private string _cihazBilgi;

        [ObservableProperty]
        private ObservableCollection<CihazHareket> _hareketler = new();

        public CihazGecmisViewModel(DatabaseService databaseService, Cihaz cihaz)
        {
            _databaseService = databaseService;
            _cihaz = cihaz;
            CihazBilgi = $"{cihaz.SeriNo} - {cihaz.CihazAdi} ({cihaz.Marka} {cihaz.Model})";
            
            _ = LoadGecmisAsync();
        }

        private async Task LoadGecmisAsync()
        {
            try
            {
                var list = await _databaseService.CihazGecmisiGetirAsync(_cihaz.Id);
                Hareketler.Clear();
                foreach (var h in list) Hareketler.Add(h);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Geçmiş yüklenirken hata: " + ex.Message);
            }
        }

        [RelayCommand]
        private void Kapat(Window window)
        {
            window.Close();
        }
    }
}
