using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Services;
using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace PersonelTakip.ViewModels;

public partial class DatabaseSettingsViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _database = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    public DatabaseSettingsViewModel()
    {
        _databaseService = new DatabaseService();
        
        var config = AppConfiguration.Instance.Database;
        Host = config.Host;
        Port = config.Port;
        Database = config.Database;
        Username = config.Username;
        Password = config.Password;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        try
        {
            // Geçici olarak ayarları güncelle ama kaydetme
            var originalConfig = AppConfiguration.Instance.Database;
            var tempHost = originalConfig.Host;
            var tempPort = originalConfig.Port;
            var tempDb = originalConfig.Database;
            var tempUser = originalConfig.Username;
            var tempPass = originalConfig.Password;

            originalConfig.Host = Host;
            originalConfig.Port = Port;
            originalConfig.Database = Database;
            originalConfig.Username = Username;
            originalConfig.Password = Password;

            bool success = await _databaseService.CheckConnectionAsync();

            if (success)
            {
                MessageBox.Show("Bağlantı başarılı!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Bağlantı başarısız. Lütfen bilgileri kontrol edin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Orijinal ayarlara geri dön (Kaydet butonuna basılana kadar kalıcı olmasın)
            originalConfig.Host = tempHost;
            originalConfig.Port = tempPort;
            originalConfig.Database = tempDb;
            originalConfig.Username = tempUser;
            originalConfig.Password = tempPass;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private void Save(Window window)
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Database) || 
            string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            MessageBox.Show("Lütfen tüm alanları doldurun.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var config = AppConfiguration.Instance.Database;
        config.Host = Host;
        config.Port = Port;
        config.Database = Database;
        config.Username = Username;
        config.Password = Password;

        try
        {
            AppConfiguration.Instance.Save();
            MessageBox.Show("Ayarlar başarıyla kaydedildi ve anında aktif edildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            window?.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            IsTesting = true;
            try
            {
                var sql = await _databaseService.GenerateBackupSqlAsync();
                await File.WriteAllTextAsync(sfd.FileName, sql);
                MessageBox.Show("Veritabanı yedeği başarıyla oluşturuldu.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yedekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsTesting = false;
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
            if (MessageBox.Show("Mevcut veriler silinecek ve yedektekiler yüklenecek. Emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                IsTesting = true;
                try
                {
                    await _databaseService.InitializeDatabaseAsync();
                    var sql = await File.ReadAllTextAsync(ofd.FileName);
                    await _databaseService.RestoreBackupSqlAsync(sql);
                    
                    // Bağlantıyı ve şemayı anında tazele
                    await _databaseService.InitializeDatabaseAsync();
                    
                    // TÜM SİSTEME HABER VER: Veriler değişti, listeleri yenileyin!
                    AppConfiguration.Instance.TriggerConfigurationChanged();
                    
                    await Task.Delay(500); // UI'ın kendine gelmesi için yarım saniye bekle
                    MessageBox.Show("VERİLER BAŞARIYLA GERİ YÜKLENDİ.\n\nSistem güncellendi, tüm listeleri kontrol edebilirsiniz.", "İşlem Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Geri yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsTesting = false;
                }
            }
        }
    }

    [RelayCommand]
    private void Cancel(Window window)
    {
        window?.Close();
    }
}
