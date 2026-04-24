using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Models;
using PersonelTakip.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using PersonelTakip.Helpers;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace PersonelTakip.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly EmailService _emailService;

    [ObservableProperty]
    private Kullanici? _currentUser;

    [ObservableProperty]
    private string _pageTitle = "Personel Takip Sistemi";

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private bool _isDataLoaded;

    [ObservableProperty]
    private bool _isSuperAdmin;

    [ObservableProperty]
    private string _connectionStatus = "Bağlantı kuruluyor...";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isSlowConnection;

    [ObservableProperty]
    private ObservableCollection<Personel> _personeller = new();

    [ObservableProperty]
    private ObservableCollection<StatItem> _santiyeStats = new();

    [ObservableProperty]
    private int _toplamPersonel;

    [ObservableProperty]
    private Personel? _selectedPersonel;

    [ObservableProperty]
    private ObservableCollection<Kullanici> _kullanicilar = new();

    [ObservableProperty]
    private Kullanici? _selectedKullanici;

    [ObservableProperty]
    private ObservableCollection<Bildirim> _bildirimlerListesi = new();

    [ObservableProperty]
    private int _unreadBildirimCount;

    [ObservableProperty]
    private bool _hasUnreadBildirimler;

    [ObservableProperty]
    private bool _isBildirimPopupOpen;

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    [ObservableProperty]
    private bool _isNotificationVisible;

    private CancellationTokenSource? _notificationCts;

    [ObservableProperty]
    private ObservableCollection<Santiye> _santiyeList = new();

    [ObservableProperty]
    private ObservableCollection<Santiye> _pasifSantiyeler = new();

    [ObservableProperty]
    private Santiye? _selectedSantiye;

    [ObservableProperty]
    private Santiye? _selectedPasifSantiye;

    [ObservableProperty]
    private ObservableCollection<LookupItem> _bolumler = new();

    [ObservableProperty]
    private ObservableCollection<LookupItem> _gorevler = new();

    [ObservableProperty]
    private ObservableCollection<LookupItem> _uyruklar = new();

    [ObservableProperty]
    private ObservableCollection<LookupItem> _paraBirimleri = new();

    [ObservableProperty]
    private LookupItem? _selectedBolum;

    [ObservableProperty]
    private LookupItem? _selectedGorev;

    [ObservableProperty]
    private LookupItem? _selectedUyruk;

    [ObservableProperty]
    private LookupItem? _selectedParaBirimi;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Santiye> _sidebarSantiyeler = new();

    [ObservableProperty]
    private string? _selectedFilterSantiye;

    [RelayCommand]
    private void SantiyeFiltrele(string? santiyeKod)
    {
        SelectedFilterSantiye = SelectedFilterSantiye == santiyeKod ? null : santiyeKod;
        PersonellerView?.Refresh();
        CalculateDashboardStats();
    }

    private void UpdateSidebarSantiyeler()
    {
        if (Personeller == null || SantiyeList == null) return;

        var aktifSantiyeIdleri = Personeller.Select(p => p.SantiyeId).Distinct().ToList();
        var filtrelenmişListe = SantiyeList.Where(s => aktifSantiyeIdleri.Contains(s.Id)).ToList();
        
        SidebarSantiyeler = new ObservableCollection<Santiye>(filtrelenmişListe);
    }

    [ObservableProperty]
    private ObservableCollection<Santiye> _sidebarKullaniciSantiyeler = new();

    [ObservableProperty]
    private string? _selectedKullaniciFilterSantiye;

    [RelayCommand]
    private void KullaniciSantiyeFiltrele(string? santiyeKod)
    {
        SelectedKullaniciFilterSantiye = SelectedKullaniciFilterSantiye == santiyeKod ? null : santiyeKod;
        KullanicilarView?.Refresh();
    }

    private void UpdateSidebarKullaniciSantiyeler()
    {
        if (Kullanicilar == null || SantiyeList == null) return;

        // Kullanıcıların şantiye kodları var ama biz listeyi id'ye göre veya isme göre yapmalıyız.
        // Kullanıcı tablosunda SantiyeId (int?) var mı? Varsa ondan, yoksa SantiyeAdi'ndan buluruz.
        var aktifSantiyeAdlari = Kullanicilar.Where(k => !string.IsNullOrEmpty(k.SantiyeAdi)).Select(k => k.SantiyeAdi).Distinct().ToList();
        var filtrelenmişListe = SantiyeList.Where(s => aktifSantiyeAdlari.Contains(s.Adi)).ToList();
        
        SidebarKullaniciSantiyeler = new ObservableCollection<Santiye>(filtrelenmişListe);
    }

    public ICollectionView? PersonellerView { get; private set; }
    public ICollectionView? KullanicilarView { get; private set; }
    public ICollectionView? SantiyeListView { get; private set; }
    public ICollectionView? BolumlerView { get; private set; }
    public ICollectionView? GorevlerView { get; private set; }
    public ICollectionView? UyruklarView { get; private set; }
    public ICollectionView? ParaBirimleriView { get; private set; }

    partial void OnSearchTextChanged(string value)
    {
        PersonellerView?.Refresh();
        KullanicilarView?.Refresh();
        SantiyeListView?.Refresh();
        BolumlerView?.Refresh();
        GorevlerView?.Refresh();
        UyruklarView?.Refresh();
        ParaBirimleriView?.Refresh();
        
        CalculateDashboardStats();
    }

    public MainViewModel()
    {
        _databaseService = new DatabaseService();
        _emailService = new EmailService();

        CurrentUser = Application.Current.Properties["Kullanici"] as Kullanici;
        IsAdmin = CurrentUser?.Rol == "Admin";
        // 'Roujin61' kullanıcı adına sahip olan kişi Süper Admin kabul edilir (büyük/küçük harf duyarsız)
        IsSuperAdmin = IsAdmin && CurrentUser?.KullaniciAdi?.Equals("Roujin61", StringComparison.OrdinalIgnoreCase) == true;
    }

    [RelayCommand]
    private async Task ToggleBildirimPopupAsync()
    {
        IsBildirimPopupOpen = !IsBildirimPopupOpen;
        
        if (IsBildirimPopupOpen && HasUnreadBildirimler)
        {
            // Okundu işaretle
            foreach (var b in BildirimlerListesi.Where(x => !x.OkunduMu))
            {
                await _databaseService.OkunmadiIseOkunduYapAsync(b.Id);
                b.OkunduMu = true;
            }
            HasUnreadBildirimler = false;
            UnreadBildirimCount = 0;
        }
    }

    [RelayCommand]
    private async Task BildirimSilAsync(Bildirim? bildirim)
    {
        if (bildirim == null) return;
        
        try
        {
            await _databaseService.BildirimSilAsync(bildirim.Id);
            BildirimlerListesi.Remove(bildirim);
            
            // Eğer silinen okunmamışsa sayacı güncelle
            if (!bildirim.OkunduMu)
            {
                UnreadBildirimCount = Math.Max(0, UnreadBildirimCount - 1);
                HasUnreadBildirimler = UnreadBildirimCount > 0;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Bildirim silinirken hata: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TumBildirimleriSilAsync()
    {
        if (BildirimlerListesi.Count == 0) return;
        if (!Confirm("Tüm bildirimler silinecek. Onaylıyor musunuz?")) return;

        try
        {
            await _databaseService.TumBildirimleriSilAsync();
            BildirimlerListesi.Clear();
            UnreadBildirimCount = 0;
            HasUnreadBildirimler = false;
        }
        catch (Exception ex)
        {
            ShowError($"Bildirimler temizlenirken hata: {ex.Message}");
        }
    }

    private async Task LoadBildirimlerAsync()
    {
        var liste = await _databaseService.GetSonBildirimlerAsync(20);
        BildirimlerListesi = new ObservableCollection<Bildirim>(liste);
        UnreadBildirimCount = BildirimlerListesi.Count(x => !x.OkunduMu);
        HasUnreadBildirimler = UnreadBildirimCount > 0;
    }

    private void StartNotificationListener()
    {
        _notificationCts?.Cancel();
        _notificationCts = new CancellationTokenSource();
        
        _ = Task.Run(async () =>
        {
            await _databaseService.StartListeningNotifications(async (tetikleyenIdStr, mesaj) => 
            {
                if (Guid.TryParse(tetikleyenIdStr, out var tetikleyenId))
                {
                    // Her durumda listeyi güncelle ve işlemi yapan kişi ben değilsem snackbar göster
                    if (CurrentUser?.Id != tetikleyenId)
                    {
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            ShowSnackbarNotification(mesaj);
                        });
                    }
                    
                    // Listeyi her halükarda güncelle (anında düşmesi için)
                    await LoadBildirimlerAsync();
                }
            }, _notificationCts.Token);
        });
    }

    private async void ShowSnackbarNotification(string message)
    {
        NotificationMessage = message;
        IsNotificationVisible = true;
        await Task.Delay(4000);
        IsNotificationVisible = false;
    }

    [RelayCommand]
    public async Task LoadPersonellerAsync()
    {
        IsBusy = true;
        try
        {
            var santiyeId = IsAdmin ? null : CurrentUser?.SantiyeId;
            var liste = await _databaseService.PersonelleriGetirAsync(santiyeId);
            Personeller = new ObservableCollection<Personel>(liste);
            
            PersonellerView = CollectionViewSource.GetDefaultView(Personeller);
            PersonellerView.Filter = (obj) =>
            {
                if (obj is not Personel p) return false;
                
                // Şantiye filtresi kontrolü
                if (!string.IsNullOrEmpty(SelectedFilterSantiye) && p.SantiyeKod != SelectedFilterSantiye)
                    return false;

                // Arama metni kontrolü
                var combined = $"{p.AdiSoyadi} {p.SantiyeKod} {p.BolumuDisplay} {p.GoreviDisplay}";
                return StringHelper.SmartSearch(combined, SearchText);
            };
            UpdateSidebarSantiyeler();
            CalculateDashboardStats();
            OnPropertyChanged(nameof(PersonellerView));
        }
        catch (Exception ex)
        {
            ShowError($"Personeller yüklenirken hata: {TranslateExceptionMessage(ex.Message)}");
            ConnectionStatus = "Bağlantı hatası";
            IsConnected = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CalculateDashboardStats()
    {
        if (PersonellerView == null) return;

        var aktifPersoneller = PersonellerView.Cast<Personel>().Where(p => p.Aktif).ToList();
        ToplamPersonel = aktifPersoneller.Count;

        var santiyeGrup = aktifPersoneller
            .GroupBy(p => string.IsNullOrWhiteSpace(p.SantiyeKod) ? "Bilinmiyor" : p.SantiyeKod)
            .Select(g => new StatItem 
            { 
                Name = g.Key, 
                Count = g.Count(),
                SubItems = new System.Collections.ObjectModel.ObservableCollection<StatItem>(
                    g.GroupBy(p => string.IsNullOrWhiteSpace(p.BolumuDisplay) ? "Bilinmiyor" : p.BolumuDisplay)
                     .Select(bg => new StatItem 
                     {
                         Name = bg.Key,
                         Count = bg.Count(),
                         SubItems = new System.Collections.ObjectModel.ObservableCollection<StatItem>(
                             bg.GroupBy(p => string.IsNullOrWhiteSpace(p.UyruguDisplay) ? "Bilinmiyor" : p.UyruguDisplay)
                               .Select(ug => new StatItem { Name = ug.Key, Count = ug.Count() })
                               .OrderByDescending(x => x.Count)
                         )
                     })
                     .OrderByDescending(x => x.Count)
                )
            })
            .OrderByDescending(x => x.Count);
            
        SantiyeStats = new ObservableCollection<StatItem>(santiyeGrup);
    }

    [RelayCommand]
    public async Task LoadKullanicilarAsync()
    {
        if (!IsAdmin) return;

        IsBusy = true;
        try
        {
            var liste = await _databaseService.KullanicilariGetirAsync();
            Kullanicilar = new ObservableCollection<Kullanici>(liste);

            KullanicilarView = CollectionViewSource.GetDefaultView(Kullanicilar);
            KullanicilarView.Filter = (obj) =>
            {
                if (obj is not Kullanici k) return false;

                if (!string.IsNullOrEmpty(SelectedKullaniciFilterSantiye) && 
                    !string.Equals(k.SantiyeAdi, SantiyeList?.FirstOrDefault(s => s.Kod == SelectedKullaniciFilterSantiye)?.Adi, StringComparison.OrdinalIgnoreCase))
                    return false;

                var combined = $"{k.KullaniciAdi} {k.Email} {k.Rol} {k.SantiyeAdi}";
                return StringHelper.SmartSearch(combined, SearchText);
            };
            OnPropertyChanged(nameof(KullanicilarView));
            UpdateSidebarKullaniciSantiyeler();
        }
        catch (Exception ex)
        {
            ShowError($"Kullanıcılar yüklenirken hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadSantiyelerAsync()
    {
        IsBusy = true;
        try
        {
            var liste = await _databaseService.SantiyeleriGetirAsync();
            SantiyeList = new ObservableCollection<Santiye>(liste);

            SantiyeListView = CollectionViewSource.GetDefaultView(SantiyeList);
            SantiyeListView.Filter = (obj) =>
            {
                if (obj is not Santiye s) return false;
                var combined = $"{s.Adi} {s.Kod} {s.Adres}";
                return StringHelper.SmartSearch(combined, SearchText);
            };
            OnPropertyChanged(nameof(SantiyeListView));
            UpdateSidebarSantiyeler();
            UpdateSidebarKullaniciSantiyeler();
        }
        catch (Exception ex)
        {
            ShowError($"Şantiyeler yüklenirken hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadLookupsAsync()
    {
        IsBusy = true;
        try
        {
            var bolumler = await _databaseService.BolumleriGetirAsync();
            var gorevler = await _databaseService.GorevleriGetirAsync();
            var uyruklar = await _databaseService.UyruklariGetirAsync();
            var paraBirimleri = await _databaseService.ParaBirimleriniGetirAsync();

            Bolumler = new ObservableCollection<LookupItem>(bolumler);
            Gorevler = new ObservableCollection<LookupItem>(gorevler);
            Uyruklar = new ObservableCollection<LookupItem>(uyruklar);
            ParaBirimleri = new ObservableCollection<LookupItem>(paraBirimleri);

            BolumlerView = CollectionViewSource.GetDefaultView(Bolumler);
            BolumlerView.Filter = (obj) => obj is LookupItem l && StringHelper.SmartSearch(l.Adi, SearchText);
            
            GorevlerView = CollectionViewSource.GetDefaultView(Gorevler);
            GorevlerView.Filter = (obj) => obj is LookupItem l && StringHelper.SmartSearch(l.Adi, SearchText);
            
            UyruklarView = CollectionViewSource.GetDefaultView(Uyruklar);
            UyruklarView.Filter = (obj) => obj is LookupItem l && StringHelper.SmartSearch(l.Adi, SearchText);
            
            ParaBirimleriView = CollectionViewSource.GetDefaultView(ParaBirimleri);
            ParaBirimleriView.Filter = (obj) => obj is LookupItem l && StringHelper.SmartSearch(l.Adi, SearchText);

            OnPropertyChanged(nameof(BolumlerView));
            OnPropertyChanged(nameof(GorevlerView));
            OnPropertyChanged(nameof(UyruklarView));
            OnPropertyChanged(nameof(ParaBirimleriView));
        }
        catch (Exception ex)
        {
            ShowError($"Lookup yüklenirken hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadAllDataAsync()
    {
        ConnectionStatus = "Veriler yükleniyor...";
        IsDataLoaded = false;

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var connectionOk = await _databaseService.CheckConnectionAsync();
            stopwatch.Stop();

            if (!connectionOk)
            {
                ConnectionStatus = "Bağlantı kurulamadı";
                IsConnected = false;
                IsSlowConnection = false;
                IsDataLoaded = false;
                return;
            }

            if (stopwatch.ElapsedMilliseconds > 5000)
            {
                ConnectionStatus = $"Bağlantı çok yavaş ({stopwatch.ElapsedMilliseconds / 1000}s)";
                IsConnected = false;
                IsSlowConnection = false;
                IsDataLoaded = false;
                return;
            }

            IsConnected = true;
            IsSlowConnection = stopwatch.ElapsedMilliseconds > 2000;
            ConnectionStatus = IsSlowConnection
                ? $"Bağlantı yavaş ({stopwatch.ElapsedMilliseconds / 1000}s)"
                : "Bağlantı hazır";

            await LoadPersonellerAsync();
            await LoadLookupsAsync();
            await LoadSantiyelerAsync();
            await LoadBildirimlerAsync();
            
            StartNotificationListener();

            if (IsAdmin)
            {
                await LoadKullanicilarAsync();
            }

            IsDataLoaded = true;
        }
        catch
        {
            ConnectionStatus = "Bağlantı hatası";
            IsConnected = false;
            IsSlowConnection = false;
            IsDataLoaded = false;
        }
    }

    [RelayCommand]
    public async Task YeniPersonelAsync()
    {
        var vm = new PersonelEditViewModel(_databaseService, CurrentUser!, IsAdmin)
        {
            Bolumler = Bolumler,
            Gorevler = Gorevler,
            Uyruklar = Uyruklar,
            ParaBirimleri = ParaBirimleri,
            SantiyeId = IsAdmin ? null : CurrentUser?.SantiyeId,
            SantiyeIdList = SantiyeList
        };

        var window = new Views.PersonelEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadPersonellerAsync();
            UpdateSidebarSantiyeler();
        }
    }

    [RelayCommand]
    public async Task DuzenlePersonelAsync()
    {
        if (SelectedPersonel == null) return;

        var vm = new PersonelEditViewModel(_databaseService, CurrentUser!, IsAdmin, SelectedPersonel)
        {
            Bolumler = Bolumler,
            Gorevler = Gorevler,
            Uyruklar = Uyruklar,
            ParaBirimleri = ParaBirimleri,
            SantiyeIdList = SantiyeList
        };

        var window = new Views.PersonelEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadPersonellerAsync();
            UpdateSidebarSantiyeler();
        }
    }

    [RelayCommand]
    public async Task SilPersonelAsync()
    {
        if (SelectedPersonel == null) return;

        if (!Confirm($"'{SelectedPersonel.AdiSoyadi}' işten çıkarılacak. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.PersonelSilAsync(SelectedPersonel.Id, CurrentUser!.Id);
            ShowSuccess("Personel işten çıkarıldı.");
            await LoadPersonellerAsync();
            UpdateSidebarSantiyeler();
        }
        catch (Exception ex)
        {
            ShowError($"Silme sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task YeniKullaniciAsync()
    {
        if (!IsAdmin) return;

        var vm = new KullaniciEditViewModel(_databaseService, _emailService, CurrentUser!)
        {
            SantiyeList = SantiyeList
        };

        var window = new Views.KullaniciEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadKullanicilarAsync();
            UpdateSidebarKullaniciSantiyeler();
        }
    }


    [RelayCommand]
    public async Task DuzenleKullaniciAsync()
    {
        if (!IsAdmin || SelectedKullanici == null) return;

        var vm = new KullaniciEditViewModel(_databaseService, _emailService, CurrentUser!, SelectedKullanici)
        {
            SantiyeList = SantiyeList
        };

        var window = new Views.KullaniciEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadKullanicilarAsync();
            UpdateSidebarKullaniciSantiyeler();
        }
    }

    [RelayCommand]
    public async Task SilKullaniciAsync()
    {
        if (!IsAdmin || SelectedKullanici == null) return;

        if (SelectedKullanici.KullaniciAdi.Equals("Roujin61", StringComparison.OrdinalIgnoreCase))
        {
            ShowError("Süper Admin hesabı (Roujin61) silinemez.");
            return;
        }

        if (!Confirm($"'{SelectedKullanici.KullaniciAdi}' kullanıcısı silinecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.KullaniciSilAsync(SelectedKullanici.Id, CurrentUser!.Id);
            ShowSuccess("Kullanıcı silindi.");
            await LoadKullanicilarAsync();
            UpdateSidebarKullaniciSantiyeler();
        }
        catch (Exception ex)
        {
            ShowError($"Silme sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SifreSifirlaAsync()
    {
        if (!IsSuperAdmin || SelectedKullanici == null) return;

        var yeniSifre = "123456";
        var mesaj = $"'{SelectedKullanici.KullaniciAdi}' kullanıcısının şifresi '{yeniSifre}' olarak sıfırlanacak. Onaylıyor musunuz?";

        if (!Confirm(mesaj)) return;

        IsBusy = true;
        try
        {
            await _databaseService.AdminParolaSifirlaAsync(SelectedKullanici.Id, yeniSifre, CurrentUser!.Id);
            ShowSuccess($"{SelectedKullanici.KullaniciAdi} kullanıcısının şifresi başarıyla sıfırlandı.\nYeni Şifre: {yeniSifre}");
        }
        catch (Exception ex)
        {
            ShowError($"Şifre sıfırlama sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        if (PersonellerView == null) return;

        var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası|*.xlsx",
            Title = "Personel Listesini Kaydet",
            FileName = $"PersonelListesi_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (sfd.ShowDialog() != true)
            return;

        // UI thread üzerinde koleksiyonu listeye çevirelim ki arka planda cross-thread hatası almayalım.
        var personellerList = PersonellerView.Cast<Personel>().ToList();
        var isSuperAdmin = IsSuperAdmin;

        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Personeller");

                // Headers
                string[] headers = { "Adı Soyadı", "Şantiye", "Bölümü", "Görevi", "Uyruğu", "İşe Giriş Tarihi", "Telefon Numarası", "Maaş", "Para Birimi", "Durum" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                }

                // Data
                int row = 2;
                foreach (var p in personellerList)
                {
                    worksheet.Cell(row, 1).Value = p.AdiSoyadi;
                    worksheet.Cell(row, 2).Value = p.SantiyeKod;
                    worksheet.Cell(row, 3).Value = p.BolumuDisplay;
                    worksheet.Cell(row, 4).Value = p.GoreviDisplay;
                    worksheet.Cell(row, 5).Value = p.UyruguDisplay;
                    worksheet.Cell(row, 6).Value = p.IseGirisTarihi?.ToString("dd.MM.yyyy");
                    worksheet.Cell(row, 7).Value = p.TelefonNumarasi;

                    if (isSuperAdmin)
                    {
                        worksheet.Cell(row, 8).Value = p.Maas;
                        worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(row, 9).Value = p.ParaBirimiDisplay;
                    }
                    else
                    {
                        worksheet.Cell(row, 8).Value = "Gizli";
                        worksheet.Cell(row, 9).Value = "-";
                    }

                    worksheet.Cell(row, 10).Value = p.Aktif ? "Aktif" : "Pasif";
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(sfd.FileName);
            });

            ShowSuccess("Excel dosyası başarıyla kaydedildi.");
        }
        catch (Exception ex)
        {
            ShowError($"Excel oluşturulurken hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task YeniSantiyeAsync()
    {
        if (!IsAdmin) return;

        var vm = new SantiyeEditViewModel(_databaseService, CurrentUser!);

        var window = new Views.SantiyeEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadSantiyelerAsync();
        }
    }

    [RelayCommand]
    public void ParolaDegistir()
    {
        var vm = new ParolaDegistirViewModel(_databaseService, CurrentUser!);
        var window = new Views.ParolaDegistirWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    [RelayCommand]
    public async Task YeniBolumAsync()
    {
        if (!IsAdmin) return;

        var vm = new LookupEditViewModel(_databaseService, "bolumler", "Bölüm");
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadLookupsAsync();
        }
    }

    [RelayCommand]
    public async Task DuzenleBolumAsync()
    {
        if (!IsAdmin || SelectedBolum == null) return;

        var vm = new LookupEditViewModel(_databaseService, "bolumler", "Bölüm", SelectedBolum);
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadLookupsAsync();
        }
    }

    [RelayCommand]
    public async Task SilBolumAsync()
    {
        if (!IsAdmin || SelectedBolum == null) return;

        if (!Confirm($"'{SelectedBolum.Adi}' bölümü silinecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.LookupSilAsync("bolumler", SelectedBolum.Id);
            ShowSuccess("Bölüm silindi.");
            await LoadLookupsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Silme sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task YeniGorevAsync()
    {
        if (!IsAdmin) return;

        var vm = new LookupEditViewModel(_databaseService, "gorevler", "Görev");
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadLookupsAsync();
        }
    }

    [RelayCommand]
    public async Task DuzenleGorevAsync()
    {
        if (!IsAdmin || SelectedGorev == null) return;

        var vm = new LookupEditViewModel(_databaseService, "gorevler", "Görev", SelectedGorev);
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadLookupsAsync();
        }
    }

    [RelayCommand]
    public async Task SilGorevAsync()
    {
        if (!IsAdmin || SelectedGorev == null) return;

        if (!Confirm($"'{SelectedGorev.Adi}' görevi silinecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.LookupSilAsync("gorevler", SelectedGorev.Id);
            ShowSuccess("Görev silindi.");
            await LoadLookupsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Silme sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task YeniUyrukAsync()
    {
        if (!IsAdmin) return;

        var vm = new LookupEditViewModel(_databaseService, "uyruklar", "Uyruk");
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadLookupsAsync();
        }
    }

    [RelayCommand]
    public async Task DuzenleUyrukAsync()
    {
        if (!IsAdmin || SelectedUyruk == null) return;

        var vm = new LookupEditViewModel(_databaseService, "uyruklar", "Uyruk", SelectedUyruk);
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadLookupsAsync();
        }
    }

    [RelayCommand]
    public async Task SilUyrukAsync()
    {
        if (!IsAdmin || SelectedUyruk == null) return;

        if (!Confirm($"'{SelectedUyruk.Adi}' uyruğu silinecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.LookupSilAsync("uyruklar", SelectedUyruk.Id);
            ShowSuccess("Uyruk silindi.");
            await LoadLookupsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Silme sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task YeniParaBirimiAsync()
    {
        if (!IsAdmin) return;

        var vm = new LookupEditViewModel(_databaseService, "para_birimleri", "Para Birimi");
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadLookupsAsync();
        }
    }

    [RelayCommand]
    public async Task DuzenleParaBirimiAsync()
    {
        if (!IsAdmin || SelectedParaBirimi == null) return;

        var vm = new LookupEditViewModel(_databaseService, "para_birimleri", "Para Birimi", SelectedParaBirimi);
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadLookupsAsync();
        }
    }

    [RelayCommand]
    public async Task SilParaBirimiAsync()
    {
        if (!IsAdmin || SelectedParaBirimi == null) return;

        if (!Confirm($"'{SelectedParaBirimi.Adi}' para birimi silinecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.LookupSilAsync("para_birimleri", SelectedParaBirimi.Id);
            ShowSuccess("Para birimi silindi.");
            await LoadLookupsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Silme sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DuzenleSantiyeAsync()
    {
        if (!IsAdmin || SelectedSantiye == null) return;

        var vm = new SantiyeEditViewModel(_databaseService, CurrentUser!, SelectedSantiye);

        var window = new Views.SantiyeEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadSantiyelerAsync();
        }
    }

    [RelayCommand]
    public async Task SilSantiyeAsync()
    {
        if (!IsAdmin || SelectedSantiye == null) return;

        if (!Confirm($"'{SelectedSantiye.Adi}' şantiyesi silinecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.SantiyeSilAsync(SelectedSantiye.Id, CurrentUser!.Id);
            ShowSuccess("Şantiye silindi.");
            await LoadSantiyelerAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Silme sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AktifEtSantiyeAsync()
    {
        if (!IsAdmin || SelectedPasifSantiye == null) return;

        if (!Confirm($"'{SelectedPasifSantiye.Adi}' şantiyesi aktifleştirilecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.SantiyeAktifEtAsync(SelectedPasifSantiye.Id, CurrentUser!.Id);
            ShowSuccess("Şantiye aktifleştirildi.");
            PasifSantiyeler.Clear();
            await LoadSantiyelerAsync();
        }
        catch (Exception ex)
        {
            ShowError($"İşlem sırasında hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadPasifSantiyelerAsync()
    {
        if (!IsAdmin) return;

        IsBusy = true;
        try
        {
            var liste = await _databaseService.SantiyeleriGetirAsync(false);
            PasifSantiyeler = new ObservableCollection<Santiye>(liste);
        }
        catch (Exception ex)
        {
            ShowError($"Yüklenirken hata: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SistemiSifirlaAsync()
    {
        if (!IsSuperAdmin) return;

        var mesaj = "DİKKAT! Bu işlem admin hesabınız hariç TÜM verileri (Personeller, Şantiyeler, Diğer Kullanıcılar, Tanımlamalar, Loglar) kalıcı olarak silecektir.\n\nSistemi sıfırlamak istediğinizden emin misiniz?";
        
        if (!Confirm(mesaj)) return;
        if (!Confirm("SON UYARI: Bu işlem geri alınamaz. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.SistemiSifirlaAsync(CurrentUser!.Id);
            ShowSuccess("Sistem başarıyla sıfırlandı. Uygulama kapatılacak.");
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            ShowError($"Sıfırlama sırasında hata oluştu: {TranslateExceptionMessage(ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void Logout()
    {
        var loginWindow = new Views.LoginWindow();
        loginWindow.Show();

        // Mevcut MainWindow'u kapat
        foreach (Window window in Application.Current.Windows)
        {
            if (window is Views.MainWindow)
            {
                window.Close();
                break;
            }
        }
    }
}
