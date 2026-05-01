using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;

using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace PersonelTakip.Monitoring.ViewModels
{
    public partial class CihazGecmisViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly Cihaz _cihaz;

        [ObservableProperty]
        private string _cihazBilgi = string.Empty;

        [ObservableProperty]
        private ObservableCollection<CihazHareket> _hareketler = [];

        public ICollectionView HareketlerView { get; private set; }

        [ObservableProperty]
        private string? _searchText = string.Empty;

        public CihazGecmisViewModel(DatabaseService databaseService, Cihaz cihaz)
        {
            _databaseService = databaseService;
            _cihaz = cihaz;
            CihazBilgi = $"{cihaz.SeriNo} - {cihaz.CihazAdi} ({cihaz.Marka} {cihaz.Model})";
            
            HareketlerView = CollectionViewSource.GetDefaultView(Hareketler);
            HareketlerView.Filter = FilterHareketler;

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

        partial void OnSearchTextChanged(string? value)
        {
            HareketlerView.Refresh();
        }

        private bool FilterHareketler(object obj)
        {
            if (obj is not CihazHareket h) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            string search = SearchText.ToLower();
            string[] terms = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string content = $"{(h.IslemTuru ?? "")} {(h.NeredenSantiyeAdi ?? "")} {(h.NereyeSantiyeAdi ?? "")} {(h.Aciklama ?? "")} {(h.KullaniciAdi ?? "")}".ToLower();

            return terms.All(term => content.Contains(term));
        }

        [RelayCommand]
        private void Kapat(Window window)
        {
            window?.Close();
        }
    }
}
