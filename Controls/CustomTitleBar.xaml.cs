using System.Windows;
using System.Windows.Controls;

namespace PersonelTakip.Monitoring.Controls;

public partial class CustomTitleBar : UserControl
{
    public static readonly DependencyProperty AdditionalContentProperty =
        DependencyProperty.Register("AdditionalContent", typeof(object), typeof(CustomTitleBar), new PropertyMetadata(null));

    public object AdditionalContent
    {
        get { return GetValue(AdditionalContentProperty); }
        set { SetValue(AdditionalContentProperty, value); }
    }

    public static readonly DependencyProperty CanMaximizeProperty =
        DependencyProperty.Register("CanMaximize", typeof(bool), typeof(CustomTitleBar), new PropertyMetadata(true));

    public bool CanMaximize
    {
        get { return (bool)GetValue(CanMaximizeProperty); }
        set { SetValue(CanMaximizeProperty, value); }
    }

    public CustomTitleBar()
    {
        InitializeComponent();
        Loaded += CustomTitleBar_Loaded;
    }

    private void CustomTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.StateChanged += (s, ev) => 
            {
                if (window.WindowState == WindowState.Maximized)
                    MaximizeButton.Content = "\xE923"; // Restore icon
                else
                    MaximizeButton.Content = "\xE922"; // Maximize icon
            };
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null) window.WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.WindowState = window.WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        window?.Close();
    }
}
