using System.Windows;
using PersonelTakip.Monitoring.ViewModels;

namespace PersonelTakip.Monitoring.Views
{
    public partial class ReportsWindow : Window
    {
        public ReportsWindow(ReportsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
