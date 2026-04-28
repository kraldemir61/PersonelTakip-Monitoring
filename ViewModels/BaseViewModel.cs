using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Input;

namespace PersonelTakip.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage = string.Empty;

    private static readonly Dictionary<string, string> ErrorTranslations = new()
    {
        ["exception while reading from stream"] = "Bağlantı koptu, lütfen tekrar deneyin",
        ["timeout"] = "İşlem zaman aşımına uğradı",
        ["connection refused"] = "Sunucuya bağlanılamıyor",
        ["connection reset"] = "Bağlantı sıfırlandı",
        ["network"] = "Ağ hatası oluştu",
        ["unable to connect"] = "Bağlantı kurulamadı",
        ["no connection could be made"] = "Sunucuya bağlanılamıyor",
        ["no such host"] = "Sunucu bulunamadı, internet bağlantınızı kontrol edin",
        ["uninitialized connectionstring"] = "Bağlantı bilgileri eksik (ConnectionString başlatılamadı)",
        ["invalid connection string"] = "Bağlantı dizesi formatı hatalı",
        ["connection string is invalid"] = "Bağlantı formatı geçersiz",
        ["login failed"] = "Giriş başarısız",
        ["invalid username"] = "Geçersiz kullanıcı adı",
        ["invalid password"] = "Geçersiz parola",
        ["bcrypt"] = "Parola doğrulaması başarısız",
        ["duplicate key"] = "Bu kayıt zaten mevcut",
        ["violates foreign key"] = "Bu kayıt başka bir kayıt tarafından kullanılıyor",
        ["not null"] = "Bu alan boş bırakılamaz",
        ["unique constraint"] = "Bu değer zaten kullanılıyor",
        ["23505"] = "Bu kayıt zaten mevcut",
        ["23503"] = "Bu kayıt başka bir kayıt tarafından kullanılıyor",
        ["cancelled"] = "İşlem iptal edildi",
        ["aborted"] = "İşlem yarıda kesildi"
    };

    protected static string TranslateExceptionMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return "Bilinmeyen bir hata oluştu";

        var lowerMsg = message.ToLower();
        foreach (var pair in ErrorTranslations)
        {
            if (lowerMsg.Contains(pair.Key.ToLower()))
                return pair.Value;
        }
        return message;
    }

    protected void ShowError(string message)
    {
        var translatedMessage = TranslateExceptionMessage(message);
        ErrorMessage = translatedMessage;
        MessageBox.Show(translatedMessage, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected static void ShowSuccess(string message)
    {
        MessageBox.Show(message, "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    protected static bool Confirm(string message)
    {
        var result = MessageBox.Show(message, "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}