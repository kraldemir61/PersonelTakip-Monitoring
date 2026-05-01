using System.Windows;
using PersonelTakip.Monitoring.ViewModels;

namespace PersonelTakip.Monitoring.Views
{
    public partial class PasifSantiyelerWindow : Window
    {
        public PasifSantiyelerWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Kapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
