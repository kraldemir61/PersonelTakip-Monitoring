using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Models;
using PersonelTakip.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PersonelTakip.ViewModels;

public partial class CihazHareketGecmisiViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly ExcelService _excelService;

    [ObservableProperty]
    private ObservableCollection<CihazHareket> _hareketler = new();

    public ICollectionView HareketlerView { get; private set; }

    [ObservableProperty]
    private string? _searchText = string.Empty;

    public CihazHareketGecmisiViewModel(DatabaseService databaseService, ExcelService excelService)
    {
        _databaseService = databaseService;
        _excelService = excelService;
        HareketlerView = CollectionViewSource.GetDefaultView(Hareketler);
        HareketlerView.Filter = FilterHareketler;

        LoadDataAsync();
    }

    private async void LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _databaseService.TumCihazHareketleriniGetirAsync();
            Hareketler.Clear();
            foreach (var item in result)
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

        string content = $"{(h.CihazSeriNo ?? "")} {(h.CihazAdi ?? "")} {(h.CihazMarka ?? "")} {(h.CihazModel ?? "")} {(h.NeredenSantiyeAdi ?? "")} {(h.NereyeSantiyeAdi ?? "")} {(h.IslemTuru ?? "")} {(h.Aciklama ?? "")} {(h.KullaniciAdi ?? "")}".ToLower();

        return terms.All(term => content.Contains(term));
    }

    [RelayCommand]
    private async Task ExcelAktarAsync()
    {
        IsBusy = true;
        try
        {
            var filteredList = HareketlerView.Cast<CihazHareket>().ToList();
            if (filteredList.Count == 0)
            {
                ShowError("Aktarılacak veri bulunamadı.");
                return;
            }

            var path = await _excelService.CihazHareketleriDisariAktarAsync(filteredList);
            if (!string.IsNullOrEmpty(path))
            {
                ShowSuccess("Veriler başarıyla Excel'e aktarıldı.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Excel aktarım hatası: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Kapat(object parameter)
    {
        if (parameter is System.Windows.Window window)
        {
            window.Close();
        }
    }
}
