using System.Windows;
using PersonelTakip.ViewModels;

namespace PersonelTakip.Views
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
