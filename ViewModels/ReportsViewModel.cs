using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Globalization;

namespace PersonelTakip.Monitoring.ViewModels
{
    public partial class ModuleItem : ObservableObject
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class ReportsViewModel : BaseViewModel
    {
        private readonly RaporService _raporService;
        private readonly DatabaseService _databaseService;
        private readonly ExcelService _excelService;
        private readonly Kullanici _currentUser;
        private readonly bool _isAdmin;
        private DataTable? _currentTable;

        [ObservableProperty]
        private List<ModuleItem> _modules = [];

        [ObservableProperty]
        private ModuleItem? _selectedModule;

        [ObservableProperty]
        private DataView? _reportDataView;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _recordCount = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ReportsViewModel(DatabaseService databaseService, ExcelService excelService, Kullanici currentUser, bool isAdmin)
        {
            _databaseService = databaseService;
            _excelService = excelService;
            _currentUser = currentUser;
            _isAdmin = isAdmin;

            _raporService = new RaporService(AppConfiguration.Instance.Database.ConnectionString);
            LoadModules();
        }

        private void LoadModules()
        {
            var displayNames = _raporService.GetModuleDisplayNames();
            Modules = displayNames.Select(kvp => new ModuleItem { Key = kvp.Key, DisplayName = kvp.Value }).ToList();
        }

        partial void OnSelectedModuleChanged(ModuleItem? value)
        {
            if (value != null)
            {
                _ = LoadReportAsync();
            }
            else
            {
                ReportDataView = null;
                RecordCount = string.Empty;
            }
            SearchText = string.Empty;
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private async Task LoadReportAsync()
        {
            if (SelectedModule == null) return;

            IsBusy = true;
            StatusMessage = "Veriler yükleniyor...";
            try
            {
                _currentTable = await _raporService.GetReportAsync(SelectedModule.Key);

                // Arama için birleşik içerik sütunu ekle
                if (!_currentTable.Columns.Contains("_SearchContent"))
                {
                    _currentTable.Columns.Add("_SearchContent", typeof(string));
                    foreach (DataRow row in _currentTable.Rows)
                    {
                        var sb = new StringBuilder();
                        foreach (var item in row.ItemArray)
                        {
                            if (item != null && item != DBNull.Value && !string.IsNullOrEmpty(item.ToString()))
                                sb.Append(item.ToString()).Append(" ");
                        }
                        row["_SearchContent"] = NormalizeText(sb.ToString());
                    }
                }

                ReportDataView = _currentTable.DefaultView;
                ApplyFilter();
                StatusMessage = $"{SelectedModule.DisplayName} yüklendi.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
                MessageBox.Show($"Rapor yüklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilter()
        {
            if (ReportDataView == null) return;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                ReportDataView.RowFilter = string.Empty;
            }
            else
            {
                var normalizedSearch = NormalizeText(SearchText);
                var terms = normalizedSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var filterSb = new StringBuilder();

                foreach (var term in terms)
                {
                    if (filterSb.Length > 0) filterSb.Append(" AND ");
                    string escapedTerm = term.Replace("'", "''");
                    filterSb.Append($"[_SearchContent] LIKE '%{escapedTerm}%'");
                }
                ReportDataView.RowFilter = filterSb.ToString();
            }

            RecordCount = $"{ReportDataView.Count} kayıt bulundu";
        }

        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.ToLower(new CultureInfo("tr-TR"))
                .Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i')
                .Replace('ö', 'o').Replace('ş', 's').Replace('ü', 'u');
        }

        [RelayCommand]
        private async Task ExportExcelAsync()
        {
            if (ReportDataView == null || SelectedModule == null || _currentTable == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                FileName = $"{SelectedModule.DisplayName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                Title = "Excel'e Aktar"
            };

            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                StatusMessage = "Excel oluşturuluyor...";
                try
                {
                    DataTable exportTable = ReportDataView.ToTable();
                    if (exportTable.Columns.Contains("_SearchContent"))
                        exportTable.Columns.Remove("_SearchContent");

                    await Task.Run(() => _raporService.ExportToExcel(exportTable, dialog.FileName, SelectedModule.DisplayName));
                    
                    StatusMessage = "Excel başarıyla kaydedildi.";
                    MessageBox.Show("Rapor başarıyla Excel'e aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Excel hatası: {ex.Message}";
                    MessageBox.Show($"Excel oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}
