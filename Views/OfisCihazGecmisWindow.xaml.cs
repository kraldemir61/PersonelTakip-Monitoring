using System.Windows;
using PersonelTakip.ViewModels;

namespace PersonelTakip.Views
{
    public partial class OfisCihazGecmisWindow : Window
    {
        public OfisCihazGecmisWindow(OfisCihazGecmisViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
