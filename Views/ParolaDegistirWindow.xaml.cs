using System.Windows;
using System.Windows.Controls;
using PersonelTakip.ViewModels;

namespace PersonelTakip.Views
{
    public partial class ParolaDegistirWindow : Window
    {
        public ParolaDegistirWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdatePlaceholders();
        }

        private void TxtEski_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ParolaDegistirViewModel vm)
            {
                vm.EskiSifre = ((PasswordBox)sender).Password;
            }
            UpdatePlaceholders();
        }

        private void TxtYeni_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ParolaDegistirViewModel vm)
            {
                vm.YeniSifre = ((PasswordBox)sender).Password;
            }
            UpdatePlaceholders();
        }

        private void TxtYeniTekrar_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ParolaDegistirViewModel vm)
            {
                vm.YeniSifreTekrar = ((PasswordBox)sender).Password;
            }
            UpdatePlaceholders();
        }

        private void UpdatePlaceholders()
        {
            UpdatePlaceholder(TxtEski);
            UpdatePlaceholder(TxtYeni);
            UpdatePlaceholder(TxtYeniTekrar);
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
    }
}
