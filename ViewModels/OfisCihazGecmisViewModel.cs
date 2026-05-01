using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;

namespace PersonelTakip.Monitoring.ViewModels
{
    public partial class OfisCihazGecmisViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelService _excelService;
        private readonly OfisCihazi? _selectedCihaz;

        [ObservableProperty]
        private ObservableCollection<CihazHareket> _hareketler = [];

        [ObservableProperty]
        private CihazHareket? _selectedHareket;

        [ObservableProperty]
        private string? _searchText = string.Empty;

        [ObservableProperty]
        private string _windowTitle = "Ofis Cihaz Hareket Geçmişi";

        public ICollectionView HareketlerView { get; private set; }

        public OfisCihazGecmisViewModel(DatabaseService databaseService, ExcelService excelService, OfisCihazi? selectedCihaz = null)
        {
            _databaseService = databaseService;
            _excelService = excelService;
            _selectedCihaz = selectedCihaz;

            if (_selectedCihaz != null)
            {
                WindowTitle = $"{_selectedCihaz.CihazAdi} - Geçmiş Detayı";
            }

            HareketlerView = CollectionViewSource.GetDefaultView(Hareketler);
            HareketlerView.Filter = FilterHareketler;

            LoadDataAsync();
        }

        private bool FilterHareketler(object obj)
        {
            if (obj is not CihazHareket h) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var search = SearchText.ToLower();
            return (h.CihazSeriNo?.ToLower().Contains(search) ?? false) ||
                   (h.CihazAdi?.ToLower().Contains(search) ?? false) ||
                   (h.PersonelAd?.ToLower().Contains(search) ?? false) ||
                   (h.IslemTuru?.ToLower().Contains(search) ?? false) ||
                   (h.Aciklama?.ToLower().Contains(search) ?? false);
        }

        partial void OnSearchTextChanged(string? value)
        {
            HareketlerView.Refresh();
        }

        private async void LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var allHistory = await _databaseService.TumCihazHareketleriniGetirAsync();
                
                // Ofis Cihazlarını filtrele: Eğer hareketin bir personel_id'si varsa bu bir ofis/zimmet hareketidir
                var officeHistory = allHistory.Where(x => x.PersonelId != null && x.PersonelId != Guid.Empty);

                // Eğer belirli bir cihaz seçiliyse sadece onun geçmişini göster
                if (_selectedCihaz != null)
                {
                    officeHistory = officeHistory.Where(x => x.CihazId == _selectedCihaz.Id);
                }

                Hareketler.Clear();
                foreach (var item in officeHistory)
                {
                    Hareketler.Add(item);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Veriler yüklenirken hata: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ExcelAktar()
        {
            try
            {
                var list = HareketlerView.Cast<CihazHareket>().ToList();
                if (!list.Any())
                {
                    ShowError("Aktarılacak veri bulunamadı."); // BaseViewModel'de ne varsa onu kullanıyoruz
                    return;
                }

                _excelService.OfisHareketListesiAktar(list);
                ShowSuccess("Excel'e aktarım başarılı.");
            }
            catch (Exception ex)
            {
                ShowError($"Excel hatası: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Kapat(object window)
        {
            if (window is System.Windows.Window w)
            {
                w.Close();
            }
        }
    }
}
