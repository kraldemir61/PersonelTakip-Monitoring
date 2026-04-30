using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Models;
using PersonelTakip.Services;

namespace PersonelTakip.ViewModels
{
    public partial class ReportsViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly ExcelService _excelService;
        private readonly Kullanici _currentUser;
        private readonly bool _isAdmin;

        [ObservableProperty]
        private string _selectedReportType;

        [ObservableProperty]
        private ObservableCollection<string> _reportTypes = new() 
        { 
            "Personel Listesi", 
            "Ölçüm Cihazları", 
            "Ofis Cihazları", 
            "Cihaz Hareket Geçmişi",
            "Şantiye Listesi"
        };

        [ObservableProperty]
        private ObservableCollection<object> _reportData = new();

        public ICollectionView ReportDataView { get; private set; }

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ReportsViewModel(DatabaseService databaseService, ExcelService excelService, Kullanici currentUser, bool isAdmin)
        {
            _databaseService = databaseService;
            _excelService = excelService;
            _currentUser = currentUser;
            _isAdmin = isAdmin;

            ReportDataView = CollectionViewSource.GetDefaultView(ReportData);
            ReportDataView.Filter = FilterReportData;

            SelectedReportType = ReportTypes[0]; // Varsayılan: Personel
        }

        partial void OnSelectedReportTypeChanged(string value)
        {
            _ = LoadReportDataAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            ReportDataView.Refresh();
        }

        private async Task LoadReportDataAsync()
        {
            IsBusy = true;
            try
            {
                ReportData.Clear();
                var santiyeId = _isAdmin ? null : _currentUser.SantiyeId;

                switch (SelectedReportType)
                {
                    case "Personel Listesi":
                        var personeller = await _databaseService.PersonelleriGetirAsync(santiyeId);
                        foreach (var p in personeller) ReportData.Add(p);
                        break;

                    case "Ölçüm Cihazları":
                        var olcumCihazlari = await _databaseService.CihazlariGetirAsync(CihazTuru.Olcum);
                        var filteredOlcum = _isAdmin ? olcumCihazlari : olcumCihazlari.Where(c => c.SantiyeId == santiyeId || c.SantiyeId == null);
                        foreach (var c in filteredOlcum) ReportData.Add(c);
                        break;

                    case "Ofis Cihazları":
                        var ofisCihazlari = await _databaseService.OfisCihazlariniGetirAsync();
                        var filteredOfis = _isAdmin ? ofisCihazlari : ofisCihazlari.Where(c => c.SantiyeId == santiyeId || !c.ZimmetliMi);
                        foreach (var c in filteredOfis) ReportData.Add(c);
                        break;

                    case "Cihaz Hareket Geçmişi":
                        var hareketler = await _databaseService.TumCihazHareketleriniGetirAsync();
                        var filteredHareket = _isAdmin ? hareketler : hareketler.Where(h => h.NeredenSantiyeId == santiyeId || h.NereyeSantiyeId == santiyeId);
                        foreach (var h in filteredHareket) ReportData.Add(h);
                        break;

                    case "Şantiye Listesi":
                        var santiyeler = await _databaseService.SantiyeleriGetirAsync(true);
                        foreach (var s in santiyeler) ReportData.Add(s);
                        break;
                }
                ReportDataView.Refresh();
            }
            catch (Exception ex)
            {
                ShowError($"Rapor yüklenirken hata: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool FilterReportData(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            // Dinamik arama (objenin özelliklerine göre)
            var props = obj.GetType().GetProperties();
            foreach (var prop in props)
            {
                var val = prop.GetValue(obj)?.ToString();
                if (val != null && val.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        [RelayCommand]
        private async Task ExportToExcelAsync()
        {
            if (ReportData.Count == 0)
            {
                ShowError("Aktarılacak veri bulunamadı.");
                return;
            }

            IsBusy = true;
            try
            {
                var list = ReportDataView.Cast<object>().ToList();
                string path = string.Empty;

                // Mevcut ExcelService metodlarını kullanarak veya yeni bir jenerik metod ekleyerek aktaralım
                switch (SelectedReportType)
                {
                    case "Personel Listesi":
                        path = await _excelService.PersonelleriDisariAktarAsync(list.Cast<Personel>().ToList());
                        break;
                    case "Ölçüm Cihazları":
                        path = await _excelService.CihazlariDisariAktarAsync(list.Cast<Cihaz>().ToList());
                        break;
                    case "Ofis Cihazları":
                        path = await _excelService.OfisCihazlariDisariAktarAsync(list.Cast<OfisCihazi>().ToList());
                        break;
                    case "Cihaz Hareket Geçmişi":
                        path = await _excelService.CihazHareketleriDisariAktarAsync(list.Cast<CihazHareket>().ToList());
                        break;
                    default:
                        // Ofis ve Şantiye için henüz ExcelService metodu yoksa uyaralım (Veya ekleyelim)
                        ShowError("Bu rapor türü için Excel aktarımı henüz hazır değil.");
                        return;
                }

                if (!string.IsNullOrEmpty(path))
                    ShowSuccess("Rapor başarıyla Excel'e aktarıldı.");
            }
            catch (Exception ex)
            {
                ShowError($"Excel hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
