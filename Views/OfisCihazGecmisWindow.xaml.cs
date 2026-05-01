using System.Windows;
using PersonelTakip.Monitoring.ViewModels;

namespace PersonelTakip.Monitoring.Views
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
