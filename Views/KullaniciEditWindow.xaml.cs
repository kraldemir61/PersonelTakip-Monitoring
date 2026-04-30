using System.Windows;
using System.Windows.Controls;
using PersonelTakip.ViewModels;

namespace PersonelTakip.Views;

public partial class KullaniciEditWindow : Window
{
    public KullaniciEditWindow()
    {
        InitializeComponent();
        Loaded += KullaniciEditWindow_Loaded;
    }

    private void KullaniciEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdatePlaceholders();
    }

    private void TxtSifre_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is KullaniciEditViewModel vm)
        {
            if (sender == TxtSifre) vm.Sifre = TxtSifre.Password;
            else if (sender == TxtSifreNew) vm.Sifre = TxtSifreNew.Password;
        }
        UpdatePlaceholders();
    }

    private void TxtSifreTekrar_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is KullaniciEditViewModel vm)
        {
            vm.SifreTekrar = TxtSifreTekrar.Password;
        }
        UpdatePlaceholders();
    }

    private void UpdatePlaceholders()
    {
        UpdatePlaceholder(TxtSifre);
        if (TxtSifreNew != null) UpdatePlaceholder(TxtSifreNew);
        UpdatePlaceholder(TxtSifreTekrar);
    }

    private void UpdatePlaceholder(PasswordBox pb)
    {
        if (pb == null) return;
        var placeholder = pb.Template.FindName("Placeholder", pb) as UIElement;
        if (placeholder != null)
        {
            placeholder.Visibility = string.IsNullOrEmpty(pb.Password) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public string Sifre => TxtSifreNew.Visibility == Visibility.Visible ? TxtSifreNew.Password : TxtSifre.Password;
    public string SifreTekrar => TxtSifreTekrar.Password;
}
