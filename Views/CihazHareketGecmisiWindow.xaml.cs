using System.Windows;
using PersonelTakip.Monitoring.ViewModels;

namespace PersonelTakip.Monitoring.Views
{
    public partial class CihazHareketGecmisiWindow : Window
    {
        public CihazHareketGecmisiWindow(CihazHareketGecmisiViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
