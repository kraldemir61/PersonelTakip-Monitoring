using PersonelTakip.Monitoring.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace PersonelTakip.Monitoring.Views;

public partial class BildirimMerkeziWindow : Window
{
    public BildirimMerkeziWindow(BildirimMerkeziViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
