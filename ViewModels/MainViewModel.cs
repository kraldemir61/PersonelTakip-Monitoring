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
using System.Linq;
using System.IO;

namespace PersonelTakip.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly EmailService _emailService;
    private readonly ExcelService _excelService;

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
    private bool _isDatabaseBusy;

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

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                // Tüm filtreleri temizle
                SearchText = string.Empty;
                SelectedFilterSantiye = null;
                SelectedKullaniciFilterSantiye = null;
                SelectedOfisFilterSantiye = null;

                // Tüm görünümleri tazele
                PersonellerView?.Refresh();
                KullanicilarView?.Refresh();
                SantiyeListView?.Refresh();
                BolumlerView?.Refresh();
                GorevlerView?.Refresh();
                UyruklarView?.Refresh();
                ParaBirimleriView?.Refresh();
                OlcumCihazlariView?.Refresh();
                OfisCihazlariView?.Refresh();
            }
        }
    }

    private int _selectedSubTabIndex;
    public int SelectedSubTabIndex
    {
        get => _selectedSubTabIndex;
        set
        {
            if (SetProperty(ref _selectedSubTabIndex, value))
            {
                SearchText = string.Empty;
            }
        }
    }

    [ObservableProperty]
    private string? _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Santiye> _sidebarSantiyeler = new();

    [ObservableProperty]
    private string? _selectedFilterSantiye;

    // Zimmet Takibi - Ölçüm Cihazları
    [ObservableProperty]
    private ObservableCollection<Cihaz> _olcumCihazlari = new();

    [ObservableProperty]
    private Cihaz? _selectedOlcumCihazi;

    public ICollectionView OlcumCihazlariView { get; private set; }

    [ObservableProperty]
    private int _toplamOlcumCihazi;

    [ObservableProperty]
    private ObservableCollection<StatItem> _olcumCihaziStats = new();

    // Zimmet Takibi - Ofis Cihazları
    [ObservableProperty]
    private ObservableCollection<OfisCihazi> _ofisCihazlari = new();

    [ObservableProperty]
    private OfisCihazi? _selectedOfisCihazi;

    public ICollectionView OfisCihazlariView { get; private set; }

    [ObservableProperty]
    private int _toplamOfisCihazi;

    [ObservableProperty]
    private bool _ofisCihaziFilterAvailable = false;

    [ObservableProperty]
    private bool _ofisCihaziFilterAssigned = false;

    [ObservableProperty] private ObservableCollection<Santiye> _sidebarOfisCihaziSantiyeler;
    [ObservableProperty] private string _selectedOfisFilterSantiye;
    [ObservableProperty] private ObservableCollection<Santiye> _sidebarOlcumCihaziSantiyeler;
    [ObservableProperty] private string _selectedOlcumFilterSantiye;

    [RelayCommand]
    private void OfisSantiyeFiltrele(string santiyeKod)
    {
        SelectedOfisFilterSantiye = SelectedOfisFilterSantiye == santiyeKod ? null : santiyeKod;
        OfisCihazlariView.Refresh();
    }

    [RelayCommand]
    private void OlcumSantiyeFiltrele(string santiyeKod)
    {
        SelectedOlcumFilterSantiye = SelectedOlcumFilterSantiye == santiyeKod ? null : santiyeKod;
        OlcumCihazlariView.Refresh();
    }

    private void UpdateSidebarOfisCihaziSantiyeler()
    {
        if (OfisCihazlari == null || SantiyeList == null) return;

        // Cihazların zimmetli olduğu personellerin şantiye kodlarını al
        var aktifSantiyeKodlari = OfisCihazlari
            .Where(c => c.ZimmetliMi && !string.IsNullOrEmpty(c.BulunduguSantiyeKod))
            .Select(c => c.BulunduguSantiyeKod)
            .Distinct()
            .ToList();

        var filtrelenmişListe = SantiyeList.Where(s => aktifSantiyeKodlari.Contains(s.Kod)).OrderBy(s => s.Kod).ToList();
        
        var sidebarListe = new ObservableCollection<Santiye>(filtrelenmişListe);

        // Eğer boşta cihaz varsa "Boşta" butonunu ekle
        if (OfisCihazlari.Any(c => !c.ZimmetliMi))
        {
            sidebarListe.Add(new Santiye { Kod = "Boşta", Adi = "Boşta" });
        }

        SidebarOfisCihaziSantiyeler = sidebarListe;
    }

    private void UpdateSidebarOlcumCihaziSantiyeler()
    {
        if (OlcumCihazlari == null || SantiyeList == null) return;

        // Ölçüm cihazlarının bulunduğu şantiye kodlarını al
        var aktifSantiyeKodlari = OlcumCihazlari
            .Where(c => !string.IsNullOrEmpty(c.SantiyeKod))
            .Select(c => c.SantiyeKod)
            .Distinct()
            .ToList();

        var filtrelenmişListe = SantiyeList.Where(s => aktifSantiyeKodlari.Contains(s.Kod)).OrderBy(s => s.Kod).ToList();
        
        var sidebarListe = new ObservableCollection<Santiye>(filtrelenmişListe);

        // Eğer boşta cihaz varsa "Boşta" butonunu ekle
        if (OlcumCihazlari.Any(c => c.SantiyeId == null))
        {
            sidebarListe.Add(new Santiye { Kod = "Boşta", Adi = "Boşta" });
        }

        SidebarOlcumCihaziSantiyeler = sidebarListe;
    }

    // Cihaz Tanımlamalar
    [ObservableProperty]
    private ObservableCollection<LookupItem> _cihazAdlari = new();
    [ObservableProperty]
    private LookupItem? _selectedCihazAd;
    public ICollectionView CihazAdlariView { get; private set; }

    [ObservableProperty]
    private ObservableCollection<LookupItem> _markalar = new();
    [ObservableProperty]
    private LookupItem? _selectedMarka;
    public ICollectionView MarkalarView { get; private set; }

    [ObservableProperty]
    private ObservableCollection<LookupItem> _modeller = new();
    [ObservableProperty]
    private LookupItem? _selectedModel;
    public ICollectionView ModellerView { get; private set; }

    [ObservableProperty]
    private ObservableCollection<LookupItem> _firmalar = new();
    [ObservableProperty]
    private LookupItem? _selectedFirma;
    public ICollectionView FirmalarView { get; private set; }

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

    partial void OnSearchTextChanged(string? value)
    {
        PersonellerView?.Refresh();
        KullanicilarView?.Refresh();
        SantiyeListView?.Refresh();
        BolumlerView?.Refresh();
        GorevlerView?.Refresh();
        UyruklarView?.Refresh();
        ParaBirimleriView?.Refresh();
        OlcumCihazlariView?.Refresh();
        OfisCihazlariView?.Refresh();
        
        CalculateDashboardStats();
        CalculateDeviceStats();
        CihazAdlariView?.Refresh();
        MarkalarView?.Refresh();
        ModellerView?.Refresh();
        FirmalarView?.Refresh();
        
        CalculateDashboardStats();
    }

    public MainViewModel()
    {
        _databaseService = new DatabaseService();
        _emailService = new EmailService();
        _excelService = new ExcelService(_databaseService);

        CurrentUser = Application.Current.Properties["Kullanici"] as Kullanici;
        IsAdmin = CurrentUser?.Rol == "Admin";
        // 'Admin' kullanıcı adına sahip olan kişi Süper Admin kabul edilir (büyük/küçük harf duyarsız)
        IsSuperAdmin = IsAdmin && CurrentUser?.KullaniciAdi?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        OlcumCihazlariView = new ListCollectionView(OlcumCihazlari);
        OlcumCihazlariView.Filter = FilterOlcumCihazlari;

        OfisCihazlariView = new ListCollectionView(OfisCihazlari);
        OfisCihazlariView.Filter = FilterOfisCihazlari;

        LoadAllDataAsync().ConfigureAwait(false);
        
        // Veritabanı veya ayarlar değiştiğinde tüm verileri otomatik tazele
        AppConfiguration.Instance.ConfigurationChanged += (s, e) => 
        {
            Application.Current.Dispatcher.Invoke(async () => {
                await LoadAllDataAsync();
            });
        };
    }

    [RelayCommand]
    private async Task ToggleBildirimPopupAsync()
    {
        IsBildirimPopupOpen = !IsBildirimPopupOpen;
        
        if (IsBildirimPopupOpen && HasUnreadBildirimler && CurrentUser != null)
        {
            // Okundu işaretle
            foreach (var b in BildirimlerListesi.Where(x => !x.OkunduMu))
            {
                await _databaseService.OkunmadiIseOkunduYapAsync(b.Id, CurrentUser.Id);
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
            if (CurrentUser != null)
            {
                await _databaseService.BildirimSilAsync(bildirim.Id, CurrentUser.Id);
                BildirimlerListesi.Remove(bildirim);
            }
            
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
            if (CurrentUser != null)
            {
                await _databaseService.TumBildirimleriSilAsync(CurrentUser.Id);
                BildirimlerListesi.Clear();
            }
            UnreadBildirimCount = 0;
            HasUnreadBildirimler = false;
        }
        catch (Exception ex)
        {
            ShowError($"Bildirimler temizlenirken hata: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenDatabaseSettings()
    {
        var win = new PersonelTakip.Views.DatabaseSettingsWindow();
        // Aktif pencereyi owner olarak belirle
        win.Owner = System.Linq.Enumerable.FirstOrDefault(System.Windows.Application.Current.Windows.Cast<System.Windows.Window>(), w => w.IsActive);
        win.ShowDialog();
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "SQL Files (*.sql)|*.sql",
            FileName = $"PersonelTakip_Yedek {DateTime.Now:dd.MM.yyyy HH.mm}.sql"
        };

        if (sfd.ShowDialog() == true)
        {
            IsDatabaseBusy = true;
            try
            {
                var sql = await _databaseService.GenerateBackupSqlAsync();
                await File.WriteAllTextAsync(sfd.FileName, sql);
                ShowSnackbarNotification("Veritabanı yedeği başarıyla oluşturuldu.");
            }
            catch (Exception ex)
            {
                ShowError($"Yedekleme hatası: {ex.Message}");
            }
            finally
            {
                IsDatabaseBusy = false;
            }
        }
    }

    [RelayCommand]
    private async Task RestoreDatabaseAsync()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "SQL Files (*.sql)|*.sql",
            Title = "Yedek Dosyası Seçin"
        };

        if (ofd.ShowDialog() == true)
        {
            if (Confirm("Mevcut veriler silinecek ve yedektekiler yüklenecek. Emin misiniz?"))
            {
                IsDatabaseBusy = true;
                try
                {
                    await _databaseService.InitializeDatabaseAsync();
                    var sql = await File.ReadAllTextAsync(ofd.FileName);
                    await _databaseService.RestoreBackupSqlAsync(sql);
                    
                    // Bağlantıyı ve şemayı anında tazele
                    await _databaseService.InitializeDatabaseAsync();
                    
                    // TÜM SİSTEME HABER VER: Veriler değişti, listeleri yenileyin!
                    AppConfiguration.Instance.TriggerConfigurationChanged();
                    
                    ShowSnackbarNotification("Veriler başarıyla geri yüklendi ve sistem güncellendi.");
                }
                catch (Exception ex)
                {
                    ShowError($"Geri yükleme hatası: {ex.Message}");
                }
                finally
                {
                    IsDatabaseBusy = false;
                }
            }
        }
    }

    private async Task LoadBildirimlerAsync()
    {
        if (CurrentUser == null) return;
        var liste = await _databaseService.GetSonBildirimlerAsync(CurrentUser.Id, 20);
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
            await _databaseService.StartListeningNotifications((tetikleyenIdStr, mesaj) => 
            {
                if (tetikleyenIdStr != null && tetikleyenIdStr.StartsWith("RESTART_TARGET:"))
                {
                    var targetIdStr = tetikleyenIdStr.Replace("RESTART_TARGET:", "");
                    if (Guid.TryParse(targetIdStr, out var targetId) && targetId == CurrentUser?.Id)
                    {
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            MessageBox.Show("Sistem yöneticisi tarafından yetkileriniz güncellendi.\nDeğişikliklerin aktif olması için program şimdi kapatılacaktır.", "Yetki Güncellemesi", MessageBoxButton.OK, MessageBoxImage.Information);
                            Application.Current.Shutdown();
                        });
                    }
                    return;
                }

                if (Guid.TryParse(tetikleyenIdStr, out var tetikleyenId))
                {
                    // Tüm güncellemeleri doğrudan UI thread'i üzerinde sırayla yapıyoruz
                    Application.Current.Dispatcher.InvokeAsync(async () => 
                    {
                        try
                        {
                            // 1. Snack bar göster
                            ShowSnackbarNotification(mesaj);
                            
                            // 2. Bildirimleri tazele
                            await LoadBildirimlerAsync();

                            // 3. Verileri tazele (Cihazlar ve Personeller)
                            await LoadCihazlarAsync();
                            OlcumCihazlariView?.Refresh();
                            OfisCihazlariView?.Refresh();
                            CalculateDeviceStats();

                            await LoadPersonellerAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Gerçek zamanlı güncelleme hatası: {ex.Message}");
                        }
                    });
                }
            }, _notificationCts.Token);
        });

        // YEDEK MEKANİZMA (Polling): LISTEN/NOTIFY bazen ağ/firewall nedeniyle takılabilir.
        // Her 30 saniyede bir listeyi manuel olarak da yenileyelim.
        _ = Task.Run(async () =>
        {
            while (!_notificationCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), _notificationCts.Token);
                    
                    var oldUnread = UnreadBildirimCount;
                    await LoadBildirimlerAsync();
                    
                    // Eğer yeni okunmamış bildirim varsa ve liste açılmadıysa bilgilendir
                    if (UnreadBildirimCount > oldUnread && !IsBildirimPopupOpen)
                    {
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            ShowSnackbarNotification("Yeni bildirimleriniz var.");
                        });
                    }
                }
                catch { /* Polling hatası kritik değil */ }
            }
        });
    }

    private async void ShowSnackbarNotification(string message)
    {
        // Önceki bildirimi temizle
        IsNotificationVisible = false;
        await Task.Delay(100);

        NotificationMessage = message;
        IsNotificationVisible = true;
        
        // 8 saniye boyunca görünür kalsın (kullanıcı görsün diye süreyi uzattık)
        await Task.Delay(8000);
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
            
            PersonellerView = new ListCollectionView(Personeller);
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
            SelectedOlcumCihazi = null;
            SelectedOfisFilterSantiye = null;
            SelectedOlcumFilterSantiye = null;
            UpdateSidebarOfisCihaziSantiyeler();
            UpdateSidebarOlcumCihaziSantiyeler();
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
        
        CalculateDeviceStats();
    }

    private void CalculateDeviceStats()
    {
        if (OlcumCihazlariView == null) return;

        var olcumCihazlari = OlcumCihazlariView.Cast<Cihaz>().ToList();
        ToplamOlcumCihazi = olcumCihazlari.Count;

        var olcumGrup = olcumCihazlari
            .GroupBy(c => string.IsNullOrWhiteSpace(c.CihazAdi) ? "Bilinmiyor" : c.CihazAdi)
            .Select(g => new StatItem 
            { 
                Name = g.Key, 
                Count = g.Count() 
            })
            .OrderByDescending(x => x.Count);

        OlcumCihaziStats = new ObservableCollection<StatItem>(olcumGrup);

        if (OfisCihazlariView == null) return;
        ToplamOfisCihazi = OfisCihazlariView.Cast<OfisCihazi>().Count();
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

            KullanicilarView = new ListCollectionView(Kullanicilar);
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
            var liste = await _databaseService.SantiyeleriGetirAsync(true);
            SantiyeList = new ObservableCollection<Santiye>(liste);

            SantiyeListView = new ListCollectionView(SantiyeList);
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

            CihazAdlari = new ObservableCollection<LookupItem>(await _databaseService.LookupGetirAsync("cihaz_adlari"));
            Markalar = new ObservableCollection<LookupItem>(await _databaseService.LookupGetirAsync("cihaz_markalari"));
            Modeller = new ObservableCollection<LookupItem>(await _databaseService.LookupGetirAsync("cihaz_modelleri"));
            Firmalar = new ObservableCollection<LookupItem>(await _databaseService.LookupGetirAsync("cihaz_firmalari"));

            BolumlerView = new ListCollectionView(Bolumler);
            BolumlerView.Filter = (obj) => StringHelper.SmartSearch(((LookupItem)obj).Adi, SearchText);
            
            GorevlerView = new ListCollectionView(Gorevler);
            GorevlerView.Filter = (obj) => StringHelper.SmartSearch(((LookupItem)obj).Adi, SearchText);
            
            UyruklarView = new ListCollectionView(Uyruklar);
            UyruklarView.Filter = (obj) => StringHelper.SmartSearch(((LookupItem)obj).Adi, SearchText);
            
            ParaBirimleriView = new ListCollectionView(ParaBirimleri);
            ParaBirimleriView.Filter = (obj) => StringHelper.SmartSearch(((LookupItem)obj).Adi, SearchText);

            CihazAdlariView = new ListCollectionView(CihazAdlari);
            CihazAdlariView.Filter = (obj) => StringHelper.SmartSearch(((LookupItem)obj).Adi, SearchText);

            MarkalarView = new ListCollectionView(Markalar);
            MarkalarView.Filter = (obj) => StringHelper.SmartSearch(((LookupItem)obj).Adi, SearchText);

            ModellerView = new ListCollectionView(Modeller);
            ModellerView.Filter = (obj) => StringHelper.SmartSearch(((LookupItem)obj).Adi, SearchText);

            FirmalarView = new ListCollectionView(Firmalar);
            FirmalarView.Filter = (obj) => StringHelper.SmartSearch(((LookupItem)obj).Adi, SearchText);

            OnPropertyChanged(nameof(BolumlerView));
            OnPropertyChanged(nameof(GorevlerView));
            OnPropertyChanged(nameof(UyruklarView));
            OnPropertyChanged(nameof(ParaBirimleriView));
            OnPropertyChanged(nameof(CihazAdlariView));
            OnPropertyChanged(nameof(MarkalarView));
            OnPropertyChanged(nameof(ModellerView));
            OnPropertyChanged(nameof(FirmalarView));
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

            await _databaseService.InitializeDatabaseAsync();
            await LoadPersonellerAsync();
            await LoadLookupsAsync();
            await LoadSantiyelerAsync();
            await LoadBildirimlerAsync();
            await LoadCihazlarAsync();
            
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

    private async Task LoadCihazlarAsync()
    {
        var selectedOlcumId = SelectedOlcumCihazi?.Id;
        var selectedOfisId = SelectedOfisCihazi?.Id;

        var olcum = await _databaseService.CihazlariGetirAsync(CihazTuru.Olcum);
        OlcumCihazlari.Clear();
        foreach (var c in olcum) OlcumCihazlari.Add(c);

        var ofis = await _databaseService.OfisCihazlariniGetirAsync();
        OfisCihazlari.Clear();
        foreach (var c in ofis) OfisCihazlari.Add(c);
        
        if (selectedOlcumId.HasValue)
            SelectedOlcumCihazi = OlcumCihazlari.FirstOrDefault(x => x.Id == selectedOlcumId.Value);
        
        if (selectedOfisId.HasValue)
            SelectedOfisCihazi = OfisCihazlari.FirstOrDefault(x => x.Id == selectedOfisId.Value);

        CalculateDeviceStats();
        UpdateSidebarOfisCihaziSantiyeler();
        UpdateSidebarOlcumCihaziSantiyeler();
    }

    private bool FilterOlcumCihazlari(object obj)
    {
        if (obj is not Cihaz c) return false;

        // Şantiye Filtresi (Sol Menü)
        if (!string.IsNullOrEmpty(SelectedOlcumFilterSantiye))
        {
            if (SelectedOlcumFilterSantiye == "Boşta")
            {
                if (c.SantiyeId != null) return false;
            }
            else
            {
                if (c.SantiyeKod != SelectedOlcumFilterSantiye)
                    return false;
            }
        }

        // Süper admin ve admin her şeyi görür, normal kullanıcılar sadece kendi şantiyesini ve boştakileri görür
        if (!IsAdmin)
        {
            bool isIdle = c.SantiyeId == null;
            bool isMySantiye = c.SantiyeId != null && c.SantiyeId == CurrentUser?.SantiyeId;
            if (!isIdle && !isMySantiye) return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        var combined = $"{c.CihazAdi} {c.SeriNo} {c.Marka} {c.Model} {c.SantiyeKod} {c.SantiyeAdi} {c.SahipFirma} {c.Durum} {c.Not}";
        return StringHelper.SmartSearch(combined, SearchText);
    }

    private bool FilterOfisCihazlari(object obj)
    {
        if (obj is not OfisCihazi c) return false;

        // Şantiye Filtresi (Sol Menü)
        if (!string.IsNullOrEmpty(SelectedOfisFilterSantiye))
        {
            if (SelectedOfisFilterSantiye == "Boşta")
            {
                if (c.ZimmetliMi) return false;
            }
            else
            {
                if (!c.ZimmetliMi || c.BulunduguSantiyeKod != SelectedOfisFilterSantiye)
                    return false;
            }
        }

        // Hızlı Durum Filtreleri (Butonlar)
        if (OfisCihaziFilterAvailable && c.ZimmetliMi) return false;
        if (OfisCihaziFilterAssigned && !c.ZimmetliMi) return false;

        // Arama Kutusu Filtresi
        var combined = $"{c.Marka} {c.Model} {c.SeriNo} {c.ZimmetliPersonelAd} {c.BulunduguSantiyeKod} {c.Ozellik}";
        return StringHelper.SmartSearch(combined, SearchText);
    }

    partial void OnOfisCihaziFilterAvailableChanged(bool value)
    {
        if (value) _ofisCihaziFilterAssigned = false;
        OnPropertyChanged(nameof(OfisCihaziFilterAssigned));
        OfisCihazlariView?.Refresh();
    }

    partial void OnOfisCihaziFilterAssignedChanged(bool value)
    {
        if (value) _ofisCihaziFilterAvailable = false;
        OnPropertyChanged(nameof(OfisCihaziFilterAvailable));
        OfisCihazlariView?.Refresh();
    }

    [RelayCommand]
    private void OfisCihaziNextFoto(OfisCihazi? cihaz)
    {
        if (cihaz == null) return;
        if (cihaz.CurrentFotoIndex < cihaz.Fotograflar.Count - 1)
            cihaz.CurrentFotoIndex++;
    }

    [RelayCommand]
    private void OfisCihaziPrevFoto(OfisCihazi? cihaz)
    {
        if (cihaz == null) return;
        if (cihaz.CurrentFotoIndex > 0)
            cihaz.CurrentFotoIndex--;
    }

    [RelayCommand]
    private async Task ZimmetleOfisCihaziAsync(OfisCihazi? cihaz)
    {
        if (cihaz == null) return;

        var window = new Views.OfisCihazZimmetWindow(cihaz, false) { Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            await LoadCihazlarAsync();
            OfisCihazlariView?.Refresh();
            ShowSuccess("Cihaz zimmetlendi.");
        }
    }

    [RelayCommand]
    private async Task IadeAlOfisCihaziAsync(OfisCihazi? cihaz)
    {
        if (cihaz == null) return;
        if (!cihaz.ZimmetliMi) return;

        var window = new Views.OfisCihazZimmetWindow(cihaz, true) { Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            await LoadCihazlarAsync();
            OfisCihazlariView?.Refresh();
            ShowSuccess("Cihaz iade alındı.");
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

            var santiyeKodu = SelectedPersonel.SantiyeKod ?? "Merkez";
            var mesaj = $"{SelectedPersonel.AdiSoyadi}, {CurrentUser.KullaniciAdi} tarafından {santiyeKodu} şantiyesinden çıkartıldı.";
            await _databaseService.BildirimEkleAsync(mesaj, CurrentUser.Id);

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
    public async Task YeniOlcumCihaziAsync()
    {
        var vm = new CihazEditViewModel(_databaseService, CihazTuru.Olcum)
        {
            SantiyeList = SantiyeList,
            IsSuperAdmin = IsSuperAdmin
        };

        var window = new Views.CihazEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadCihazlarAsync();
        }
    }

    [RelayCommand]
    public async Task DuzenleOlcumCihaziAsync()
    {
        if (SelectedOlcumCihazi == null) return;

        // Admin değilse ve cihaz kendi şantiyesinde değilse VE boşta da değilse izin verme
        if (!IsAdmin && SelectedOlcumCihazi.SantiyeId != null && SelectedOlcumCihazi.SantiyeId != CurrentUser?.SantiyeId)
        {
            ShowError("Bu cihaz üzerinde işlem yapma yetkiniz bulunmamaktadır.");
            return;
        }

        var vm = new CihazEditViewModel(_databaseService, CihazTuru.Olcum, SelectedOlcumCihazi)
        {
            SantiyeList = SantiyeList,
            IsSuperAdmin = IsSuperAdmin
        };

        var window = new Views.CihazEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadCihazlarAsync();
        }
    }

    [RelayCommand]
    public async Task OlcumCihaziHareketAsync()
    {
        if (SelectedOlcumCihazi == null) return;

        // Admin değilse kontrol et
        if (!IsAdmin)
        {
            // Cihaz bir şantiyeye kayıtlıysa ve o şantiye kullanıcının şantiyesi değilse hata ver
            if (SelectedOlcumCihazi.SantiyeId != null && SelectedOlcumCihazi.SantiyeId != CurrentUser?.SantiyeId)
            {
                ShowError("Bu cihaz üzerinde işlem yapma yetkiniz bulunmamaktadır.");
                return;
            }
        }

        var vm = new CihazHareketViewModel(_databaseService, CurrentUser!, SelectedOlcumCihazi)
        {
            SantiyeList = SantiyeList,
            IsSuperAdmin = IsAdmin // Adminler de her yere taşıyabilsin
        };

        // Eğer cihaz boştaysa ve kullanıcı admin değilse, hedefi direkt kendi şantiyesi yapalım
        if (!IsAdmin && SelectedOlcumCihazi.SantiyeId == null)
        {
            vm.NereyeSantiyeId = CurrentUser?.SantiyeId;
        }

        var window = new Views.CihazHareketWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadCihazlarAsync();
        }
    }

    [RelayCommand]
    public void OlcumCihaziGecmis()
    {
        if (SelectedOlcumCihazi == null)
        {
            var vm = new CihazHareketGecmisiViewModel(_databaseService, _excelService);
            var window = new Views.CihazHareketGecmisiWindow(vm);
            window.ShowDialog();
            return;
        }

        var vmOld = new CihazGecmisViewModel(_databaseService, SelectedOlcumCihazi);
        var windowOld = new Views.CihazGecmisWindow { DataContext = vmOld };
        windowOld.ShowDialog();
    }

    [RelayCommand]
    public async Task SilOlcumCihaziAsync()
    {
        if (SelectedOlcumCihazi == null) return;

        // Sadece Süper Admin silme yapabilir
        if (!IsSuperAdmin)
        {
            ShowError("Cihaz silme yetkisi sadece Süper Admin'e aittir.");
            return;
        }
        
        var result = MessageBox.Show($"{SelectedOlcumCihazi.SeriNo} seri nolu cihazı silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await _databaseService.CihazSilAsync(SelectedOlcumCihazi.Id);
            await LoadCihazlarAsync();
        }
    }

    [RelayCommand]
    public async Task YeniOfisCihaziAsync()
    {
        var vm = new OfisCihazEditViewModel(_databaseService);

        var window = new Views.OfisCihazEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadCihazlarAsync();
        }
    }

    [RelayCommand]
    public async Task DuzenleOfisCihaziAsync()
    {
        if (SelectedOfisCihazi == null) return;

        var vm = new OfisCihazEditViewModel(_databaseService, SelectedOfisCihazi);

        var window = new Views.OfisCihazEditWindow { DataContext = vm };
        if (window.ShowDialog() == true)
        {
            await LoadCihazlarAsync();
        }
    }

    [RelayCommand]
    public async Task SilOfisCihaziAsync()
    {
        if (SelectedOfisCihazi == null) return;
        
        var result = MessageBox.Show($"{SelectedOfisCihazi.SeriNo} seri nolu cihazı silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await _databaseService.CihazSilAsync(SelectedOfisCihazi.Id);
            await LoadCihazlarAsync();
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

        if (SelectedKullanici.KullaniciAdi.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            ShowError("Süper Admin hesabı (Admin) silinemez.");
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

    // CİHAZ TANIMLAMALARI KOMUTLARI
    [RelayCommand] public async Task YeniCihazAdAsync() => await ManageLookupAsync("cihaz_adlari", "Cihaz Adı");
    [RelayCommand] public async Task DuzenleCihazAdAsync() => await ManageLookupAsync("cihaz_adlari", "Cihaz Adı", SelectedCihazAd);
    [RelayCommand] public async Task SilCihazAdAsync() => await DeleteLookupAsync("cihaz_adlari", "Cihaz Adı", SelectedCihazAd);

    [RelayCommand] public async Task YeniMarkaAsync() => await ManageLookupAsync("cihaz_markalari", "Marka");
    [RelayCommand] public async Task DuzenleMarkaAsync() => await ManageLookupAsync("cihaz_markalari", "Marka", SelectedMarka);
    [RelayCommand] public async Task SilMarkaAsync() => await DeleteLookupAsync("cihaz_markalari", "Marka", SelectedMarka);

    [RelayCommand] public async Task YeniModelAsync() => await ManageLookupAsync("cihaz_modelleri", "Model");
    [RelayCommand] public async Task DuzenleModelAsync() => await ManageLookupAsync("cihaz_modelleri", "Model", SelectedModel);
    [RelayCommand] public async Task SilModelAsync() => await DeleteLookupAsync("cihaz_modelleri", "Model", SelectedModel);

    [RelayCommand] public async Task YeniFirmaAsync() => await ManageLookupAsync("cihaz_firmalari", "Firma");
    [RelayCommand] public async Task DuzenleFirmaAsync() => await ManageLookupAsync("cihaz_firmalari", "Firma", SelectedFirma);
    [RelayCommand] public async Task SilFirmaAsync() => await DeleteLookupAsync("cihaz_firmalari", "Firma", SelectedFirma);

    private async Task ManageLookupAsync(string table, string title, LookupItem? item = null)
    {
        if (!IsAdmin) return;
        var vm = new LookupEditViewModel(_databaseService, table, title, item);
        var window = new Views.LookupEditWindow { DataContext = vm };
        if (window.ShowDialog() == true) await LoadLookupsAsync();
    }

    private async Task DeleteLookupAsync(string table, string title, LookupItem? item)
    {
        if (!IsAdmin || item == null) return;
        if (!Confirm($"'{item.Adi}' {title.ToLower()} silinecek. Onaylıyor musunuz?")) return;
        try {
            await _databaseService.LookupSilAsync(table, item.Id);
            ShowSuccess($"{title} silindi.");
            await LoadLookupsAsync();
        } catch (Exception ex) {
            ShowError($"Silme hatası: {ex.Message}");
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
        if (!IsSuperAdmin) return;

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
    public async Task AktiflestirSantiyeAsync(Santiye santiye)
    {
        if (santiye == null) return;
        
        try
        {
            await _databaseService.SantiyeAktiflestirAsync(santiye.Id);
            await LoadPasifSantiyelerAsync();
            await LoadSantiyelerAsync();
            ShowSuccess($"{santiye.Adi} şantiyesi tekrar aktifleştirildi.");
        }
        catch (Exception ex)
        {
            ShowError($"Aktifleştirme hatası: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ResetDatabaseAsync()
    {
        if (!IsSuperAdmin) return;
        
        if (!Confirm("DİKKAT: Veritabanındaki tüm Personeller, Cihazlar, Hareket Geçmişi ve Bildirimler silinecektir. \n\nSadece Tanımlamalar (Şantiyeler, Bölümler vb.) ve Süper Admin hesabı kalacaktır. \n\nBu işlem geri alınamaz! Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            await _databaseService.ExecuteSqlAsync("DELETE FROM bildirimler;");
            await _databaseService.ExecuteSqlAsync("DELETE FROM audit_log;");
            await _databaseService.ExecuteSqlAsync("DELETE FROM cihaz_hareketleri;");
            await _databaseService.ExecuteSqlAsync("DELETE FROM cihazlar;");
            await _databaseService.ExecuteSqlAsync("DELETE FROM personeller;");
            await _databaseService.ExecuteSqlAsync("DELETE FROM kullanicilar WHERE LOWER(kullanici_adi) != 'admin';");
            
            ShowSuccess("Veritabanı başarıyla temizlendi.");
            
            // Verileri yenile
            await LoadAllDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Temizleme sırasında hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task OpenPasifSantiyelerAsync()
    {
        if (!IsSuperAdmin) return;
        await LoadPasifSantiyelerAsync();
        var window = new Views.PasifSantiyelerWindow(this);
        window.ShowDialog();
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
        Application.Current.MainWindow = loginWindow;
        loginWindow.Show();

        // Diğer pencereleri (MainWindow vb.) kapat
        var otherWindows = Application.Current.Windows.Cast<Window>()
            .Where(w => w != loginWindow).ToList();
            
        foreach (Window window in otherWindows)
        {
            if (window is Views.MainWindow || window is Views.LoginWindow == false)
            {
                window.Close();
            }
        }
    }

    #region Excel İşlemleri

    [RelayCommand]
    private void ExcelSablonIndir()
    {
        if (!IsSuperAdmin) return;
        _excelService.TemplateIndir();
    }

    [RelayCommand]
    private async Task ExcelTopluYukleAsync()
    {
        if (!IsSuperAdmin) return;

        var (personeller, error) = await _excelService.ExceldenOkuAsync(isUpdate: false);
        if (!string.IsNullOrEmpty(error))
        {
            ShowError($"Excel okuma hatası: {error}");
            return;
        }

        if (personeller.Count == 0)
        {
            ShowError("Excel dosyasında geçerli personel kaydı bulunamadı.");
            return;
        }

        if (!Confirm($"{personeller.Count} adet yeni personel eklenecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            // Eksik tanımlamaları (bölüm, görev vb.) kontrol et ve ekle
            await EnsurePersonelLookupsAsync(personeller);

            int count = 0;
            foreach (var p in personeller)
            {
                await _databaseService.PersonelOlusturAsync(p);
                count++;
            }
            ShowSuccess($"{count} adet personel başarıyla eklendi.");
            await LoadAllDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Toplu yükleme sırasında hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExcelDisariAktarAsync()
    {
        if (!IsSuperAdmin) return;
        
        IsBusy = true;
        try
        {
            var personeller = await _databaseService.PersonelleriGetirAsync();
            var path = await _excelService.PersonelleriDisariAktarAsync(personeller);
            if (!string.IsNullOrEmpty(path))
            {
                ShowSuccess("Veriler başarıyla dışarı aktarıldı.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Dışarı aktarma hatası: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExcelTopluGuncelleAsync()
    {
        if (!IsSuperAdmin) return;

        var (personeller, error) = await _excelService.ExceldenOkuAsync(isUpdate: true);
        if (!string.IsNullOrEmpty(error))
        {
            ShowError($"Excel okuma hatası: {error}");
            return;
        }

        if (personeller.Count == 0)
        {
            ShowError("Excel dosyasında güncellenecek geçerli personel kaydı bulunamadı. ID sütununu kontrol edin.");
            return;
        }

        if (!Confirm($"{personeller.Count} adet personel kaydı güncellenecek. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            // Eksik tanımlamaları (bölüm, görev vb.) kontrol et ve ekle
            await EnsurePersonelLookupsAsync(personeller);

            int count = 0;
            foreach (var p in personeller)
            {
                await _databaseService.PersonelGuncelleAsync(p);
                count++;
            }
            ShowSuccess($"{count} adet personel başarıyla güncellendi.");
            await LoadAllDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Toplu güncelleme sırasında hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Cihaz Excel İşlemleri

    [RelayCommand]
    private void ExcelCihazSablonIndir()
    {
        if (!IsSuperAdmin) return;
        _excelService.CihazTemplateIndir();
    }

    [RelayCommand]
    private async Task ExcelCihazTopluYukleAsync()
    {
        if (!IsSuperAdmin) return;

        var (cihazlar, error) = await _excelService.CihazExceldenOkuAsync(isUpdate: false);
        if (!string.IsNullOrEmpty(error))
        {
            ShowError($"Excel okuma hatası: {error}");
            return;
        }

        if (cihazlar.Count == 0) return;

        if (!Confirm($"{cihazlar.Count} adet cihaz sisteme toplu olarak eklenecek. Tanımlı olmayan Cihaz Adı, Marka, Model ve Firmalar otomatik olarak eklenecektir. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            // 1. Tanımlamaları kontrol et ve eksikleri ekle
            await EnsureCihazLookupsAsync(cihazlar);

            // 2. Cihazları ekle
            foreach (var c in cihazlar)
            {
                await _databaseService.CihazEkleAsync(c);
            }
            ShowSuccess($"{cihazlar.Count} adet cihaz başarıyla eklendi.");
            await LoadCihazlarAsync();
            await LoadLookupsAsync(); // Tanımlamalar güncellenmiş olabilir
        }
        catch (Exception ex)
        {
            ShowError($"Yükleme sırasında hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EnsureCihazLookupsAsync(List<Cihaz> cihazlar)
    {
        // Mevcutları yükle
        var adlar = await _databaseService.LookupGetirAsync("cihaz_adlari");
        var markalar = await _databaseService.LookupGetirAsync("cihaz_markalari");
        var modeller = await _databaseService.LookupGetirAsync("cihaz_modelleri");
        var firmalar = await _databaseService.LookupGetirAsync("cihaz_firmalari");

        foreach (var c in cihazlar)
        {
            if (!string.IsNullOrWhiteSpace(c.CihazAdi))
                await CheckAndAddLookupAsync("cihaz_adlari", adlar, c.CihazAdi);

            if (!string.IsNullOrWhiteSpace(c.Marka))
                await CheckAndAddLookupAsync("cihaz_markalari", markalar, c.Marka);

            if (!string.IsNullOrWhiteSpace(c.Model))
                await CheckAndAddLookupAsync("cihaz_modelleri", modeller, c.Model);

            if (!string.IsNullOrWhiteSpace(c.SahipFirma))
                await CheckAndAddLookupAsync("cihaz_firmalari", firmalar, c.SahipFirma);
        }
    }

    private async Task EnsurePersonelLookupsAsync(List<Personel> personeller)
    {
        var bolumler = await _databaseService.LookupGetirAsync("bolumler");
        var gorevler = await _databaseService.LookupGetirAsync("gorevler");
        var uyruklar = await _databaseService.LookupGetirAsync("uyruklar");
        var paraBirimleri = await _databaseService.LookupGetirAsync("para_birimleri");

        foreach (var p in personeller)
        {
            if (!string.IsNullOrWhiteSpace(p.BolumuDisplay))
                p.Bolumu = await CheckAndAddLookupAsync("bolumler", bolumler, p.BolumuDisplay);

            if (!string.IsNullOrWhiteSpace(p.GoreviDisplay))
                p.Gorevi = await CheckAndAddLookupAsync("gorevler", gorevler, p.GoreviDisplay);

            if (!string.IsNullOrWhiteSpace(p.UyruguDisplay))
                p.Uyrugu = await CheckAndAddLookupAsync("uyruklar", uyruklar, p.UyruguDisplay);
            
            if (!string.IsNullOrWhiteSpace(p.ParaBirimiDisplay))
                p.ParaBirimi = await CheckAndAddLookupAsync("para_birimleri", paraBirimleri, p.ParaBirimiDisplay);
        }
    }

    private async Task<int?> CheckAndAddLookupAsync(string table, List<LookupItem> list, string value)
    {
        var normalizedValue = Helpers.StringHelper.NormalizeTurkish(value);
        var existing = list.FirstOrDefault(l => Helpers.StringHelper.NormalizeTurkish(l.Adi) == normalizedValue);
        if (existing != null) return existing.Id;

        // Veritabanına ekle
        var newId = await _databaseService.LookupOlusturAsync(table, value);
        // Listeye de ekle ki aynı excelde tekrar edenler için tekrar veritabanına gitmesin
        list.Add(new LookupItem { Id = newId, Adi = value });
        return newId;
    }

    [RelayCommand]
    private async Task ExcelCihazDisariAktarAsync()
    {
        if (!IsSuperAdmin) return;

        IsBusy = true;
        try
        {
            var cihazlar = await _databaseService.CihazlariGetirAsync(CihazTuru.Olcum);
            var path = await _excelService.CihazlariDisariAktarAsync(cihazlar);
            if (!string.IsNullOrEmpty(path))
            {
                ShowSuccess("Cihaz verileri başarıyla dışarı aktarildi.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Dışarı aktarma hatası: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExcelCihazTopluGuncelleAsync()
    {
        if (!IsSuperAdmin) return;

        var (cihazlar, error) = await _excelService.CihazExceldenOkuAsync(isUpdate: true);
        if (!string.IsNullOrEmpty(error))
        {
            ShowError($"Excel okuma hatası: {error}");
            return;
        }

        if (cihazlar.Count == 0) return;

        if (!Confirm($"{cihazlar.Count} adet cihaz kaydı güncellenecek. Yeni tanımlanan Cihaz Adı, Marka, Model ve Firmalar otomatik olarak eklenecektir. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            // 1. Tanımlamaları kontrol et ve eksikleri ekle
            await EnsureCihazLookupsAsync(cihazlar);

            // 2. Cihazları güncelle
            foreach (var c in cihazlar)
            {
                await _databaseService.CihazGuncelleAsync(c);
            }
            ShowSuccess($"{cihazlar.Count} adet cihaz başarıyla güncellendi.");
            await LoadCihazlarAsync();
            await LoadLookupsAsync(); // Tanımlamalar güncellenmiş olabilir
        }
        catch (Exception ex)
        {
            ShowError($"Güncelleme sırasında hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
    #region Ofis Cihaz Excel İşlemleri

    [RelayCommand]
    private void ExcelOfisCihazSablonIndir()
    {
        if (!IsSuperAdmin) return;
        _excelService.OfisCihazTemplateIndir();
    }

    [RelayCommand]
    private async Task ExcelOfisCihazTopluYukleAsync()
    {
        if (!IsSuperAdmin) return;

        var (cihazlar, error) = await _excelService.OfisCihazExceldenOkuAsync(isUpdate: false);
        if (!string.IsNullOrEmpty(error))
        {
            ShowError($"Excel okuma hatası: {error}");
            return;
        }

        if (cihazlar.Count == 0) return;

        if (!Confirm($"{cihazlar.Count} adet ofis cihazı sisteme toplu olarak eklenecek. Tanımlı olmayan Cihaz Adı, Marka ve Modeller otomatik olarak eklenecektir. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            // 1. Tanımlamaları kontrol et ve eksikleri ekle
            await EnsureOfisCihazLookupsAsync(cihazlar);

            // 2. Cihazları ekle
            foreach (var c in cihazlar)
            {
                await _databaseService.OfisCihaziEkleAsync(c);
            }
            ShowSuccess($"{cihazlar.Count} adet ofis cihazı başarıyla eklendi.");
            await LoadCihazlarAsync();
            await LoadLookupsAsync(); 
        }
        catch (Exception ex)
        {
            ShowError($"Yükleme sırasında hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EnsureOfisCihazLookupsAsync(List<OfisCihazi> cihazlar)
    {
        var adlar = await _databaseService.LookupGetirAsync("cihaz_adlari");
        var markalar = await _databaseService.LookupGetirAsync("cihaz_markalari");
        var modeller = await _databaseService.LookupGetirAsync("cihaz_modelleri");

        foreach (var c in cihazlar)
        {
            if (!string.IsNullOrWhiteSpace(c.CihazAdi))
                await CheckAndAddLookupAsync("cihaz_adlari", adlar, c.CihazAdi);

            if (!string.IsNullOrWhiteSpace(c.Marka))
                await CheckAndAddLookupAsync("cihaz_markalari", markalar, c.Marka);

            if (!string.IsNullOrWhiteSpace(c.Model))
                await CheckAndAddLookupAsync("cihaz_modelleri", modeller, c.Model);
        }
    }

    [RelayCommand]
    private async Task ExcelOfisCihazDisariAktarAsync()
    {
        if (!IsSuperAdmin) return;

        IsBusy = true;
        try
        {
            var cihazlar = await _databaseService.OfisCihazlariniGetirAsync();
            var path = await _excelService.OfisCihazlariDisariAktarAsync(cihazlar);
            if (!string.IsNullOrEmpty(path))
            {
                ShowSuccess("Ofis cihaz verileri başarıyla dışarı aktarıldı.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Dışarı aktarma hatası: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExcelOfisCihazTopluGuncelleAsync()
    {
        if (!IsSuperAdmin) return;

        var (cihazlar, error) = await _excelService.OfisCihazExceldenOkuAsync(isUpdate: true);
        if (!string.IsNullOrEmpty(error))
        {
            ShowError($"Excel okuma hatası: {error}");
            return;
        }

        if (cihazlar.Count == 0) return;

        if (!Confirm($"{cihazlar.Count} adet ofis cihazı kaydı güncellenecek. Yeni tanımlanan Cihaz Adı, Marka ve Modeller otomatik olarak eklenecektir. Onaylıyor musunuz?")) return;

        IsBusy = true;
        try
        {
            // 1. Tanımlamaları kontrol et ve eksikleri ekle
            await EnsureOfisCihazLookupsAsync(cihazlar);

            // 2. Cihazları güncelle
            foreach (var c in cihazlar)
            {
                await _databaseService.OfisCihaziGuncelleAsync(c);
            }
            ShowSuccess($"{cihazlar.Count} adet ofis cihazı başarıyla güncellendi.");
            await LoadCihazlarAsync();
            await LoadLookupsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Güncelleme sırasında hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
