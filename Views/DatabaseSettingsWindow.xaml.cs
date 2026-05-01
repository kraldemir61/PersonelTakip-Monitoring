using System.Windows;
using PersonelTakip.Monitoring.ViewModels;

namespace PersonelTakip.Monitoring.Views;

public partial class DatabaseSettingsWindow : Window
{
    public DatabaseSettingsWindow()
    {
        InitializeComponent();
        DataContext = new DatabaseSettingsViewModel();
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            this.DragMove();
    }
}
