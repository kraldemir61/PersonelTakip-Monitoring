using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonelTakip.Services;
using System.Windows;

namespace PersonelTakip.ViewModels;

public partial class DatabaseSettingsViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private string _host;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _database;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _password;

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
            MessageBox.Show("Ayarlar başarıyla kaydedildi. Değişikliklerin aktif olması için programı yeniden başlatmanız gerekebilir.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            window?.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Cancel(Window window)
    {
        window?.Close();
    }
}
