using System.Windows;
using System.Windows.Controls;
using PersonelTakip.ViewModels;

namespace PersonelTakip.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow()
    {
        InitializeComponent();
        _viewModel = new LoginViewModel();
        _viewModel.LoginSuccessful += OnLoginSuccessful;
        _viewModel.RegistrationSuccessful += OnRegistrationSuccessful;
        DataContext = _viewModel;
    }

    private void OnRegistrationSuccessful()
    {
        RbGiris.IsChecked = true;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TxtPassword.Focus();
    }

    private void RbGiris_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelGiris != null && PanelKayit != null)
        {
            PanelGiris.Visibility = Visibility.Visible;
            PanelKayit.Visibility = Visibility.Collapsed;
        }
    }

    private void RbKayit_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelGiris != null && PanelKayit != null)
        {
            PanelGiris.Visibility = Visibility.Collapsed;
            PanelKayit.Visibility = Visibility.Visible;
        }
    }

    private async void BtnGiris_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LoginKullaniciAdi = TxtEmail.Text;
        _viewModel.Password = TxtPassword.Password;
        await _viewModel.LoginCommand.ExecuteAsync(null);
    }

    private async void BtnKayit_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RegisterKullaniciAdi = TxtKullaniciAdi.Text;
        _viewModel.Password = TxtRegisterSifre.Password;

        await _viewModel.RegisterCommand.ExecuteAsync(null);
    }

    private void BtnGeriDon_Click(object sender, RoutedEventArgs e)
    {
        TxtEmail.Text = "";
        TxtPassword.Password = "";
        TxtKullaniciAdi.Text = "";
        TxtRegisterSifre.Password = "";
        TxtRegisterSifreTekrar.Password = "";
        RbKayit.IsChecked = true;
    }

    private void OnLoginSuccessful(Models.Kullanici kullanici)
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        this.Close();
    }
}
