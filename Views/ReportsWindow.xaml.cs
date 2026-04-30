using System.Windows;
using PersonelTakip.ViewModels;

namespace PersonelTakip.Views
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
