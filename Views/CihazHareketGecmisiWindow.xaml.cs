using System.Windows;
using PersonelTakip.ViewModels;

namespace PersonelTakip.Views
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
