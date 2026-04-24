using System.Windows;
using PersonelTakip.ViewModels;

namespace PersonelTakip.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        Loaded += async (s, e) =>
        {
            await viewModel.LoadAllDataAsync();
        };
    }
}
