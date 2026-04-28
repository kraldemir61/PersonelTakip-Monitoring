using System;
using System.Windows;
using PersonelTakip.Services;

namespace PersonelTakip.Views
{
    public partial class LicenseWindow : Window
    {
        public LicenseWindow()
        {
            InitializeComponent();
            TxtHardwareId.Text = LicenseManager.GetHardwareId();
            CheckDemoStatus();
        }

        private async void CheckDemoStatus()
        {
            var summary = await LicenseManager.GetDemoSummaryAsync();
            if (summary.Status == LicenseManager.DemoStatus.Active)
            {
                TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
                TxtStatus.Text = summary.Message;
                BtnTrial.Content = "Demo modda devam et";
            }
            else if (summary.Status == LicenseManager.DemoStatus.Expired || summary.Status == LicenseManager.DemoStatus.RollbackDetected)
            {
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
                TxtStatus.Text = summary.Message;
                BtnTrial.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(TxtHardwareId.Text);
            MessageBox.Show("Donanım ID kopyalandı!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            string key = TxtLicenseKey.Text;
            if (LicenseManager.ValidateLicense(key))
            {
                await LicenseManager.SaveLicenseAsync(key);
                MessageBox.Show("Program başarıyla etkinleştirildi!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                TxtStatus.Text = "Geçersiz lisans anahtarı!";
            }
        }

        private async void BtnTrial_Click(object sender, RoutedEventArgs e)
        {
            var summary = await LicenseManager.GetDemoSummaryAsync();
            if (summary.Status == LicenseManager.DemoStatus.None)
            {
                await LicenseManager.StartDemoAsync();
                this.DialogResult = true;
                this.Close();
            }
            else if (summary.Status == LicenseManager.DemoStatus.Active)
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
