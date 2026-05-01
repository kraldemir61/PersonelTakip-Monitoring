using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PersonelTakip.Monitoring.ViewModels;

public partial class CihazHareketGecmisiViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly ExcelService _excelService;
    private readonly Kullanici _currentUser;
    private readonly bool _isAdmin;

    [ObservableProperty]
    private ObservableCollection<CihazHareket> _hareketler = [];

    public ICollectionView HareketlerView { get; private set; }

    [ObservableProperty]
    private string? _searchText = string.Empty;

    [ObservableProperty]
    private CihazTuru? _filtreTuru;

    public CihazHareketGecmisiViewModel(DatabaseService databaseService, ExcelService excelService, Kullanici currentUser, bool isAdmin, CihazTuru? filtreTuru = null)
    {
        _databaseService = databaseService;
        _excelService = excelService;
        _currentUser = currentUser;
        _isAdmin = isAdmin;
        FiltreTuru = filtreTuru;
        
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
            
            // Sadece Ölçüm Cihazlarını filtrele (CihazTuru = 0 / Olcum) ve personel zimmeti olmayanları getir
            var filteredResult = result.Where(x => x.CihazTuru == CihazTuru.Olcum && string.IsNullOrEmpty(x.PersonelAd));

            // Eğer admin değilse sadece kendi şantiyesinin hareketlerini görsün
            if (!_isAdmin && _currentUser.SantiyeId != null)
            {
                filteredResult = filteredResult.Where(x => x.NeredenSantiyeId == _currentUser.SantiyeId || x.NereyeSantiyeId == _currentUser.SantiyeId);
            }

            // Eğer cihazId bazlı bir filtre varsa (tekil geçmiş)
            if (FiltreTuru.HasValue)
            {
                // FiltreTuru burada cihazId mi yoksa başka bir şey mi kontrol etmeliyiz
                // Ama genel listede sadece ölçüm cihazlarını görmek istediğimiz kesin
            }

            foreach (var item in filteredResult)
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
