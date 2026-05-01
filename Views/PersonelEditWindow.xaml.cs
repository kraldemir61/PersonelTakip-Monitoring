using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace PersonelTakip.Monitoring.Views;

public partial class PersonelEditWindow : Window
{
    public PersonelEditWindow()
    {
        InitializeComponent();
    }

    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9,.]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    private void DatePicker_CalendarOpened(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            if (sender is DatePicker dp)
                FixCalendarColors(dp);
        }));
    }

    private void FixCalendarColors(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

            if (child is System.Windows.Controls.TextBlock tb)
            {
                tb.Foreground = System.Windows.Media.Brushes.White;
            }
            else if (child is System.Windows.Controls.Primitives.CalendarDayButton dayBtn)
            {
                dayBtn.Foreground = System.Windows.Media.Brushes.White;
            }
            else if (child is System.Windows.Controls.Primitives.CalendarButton calBtn)
            {
                calBtn.Foreground = System.Windows.Media.Brushes.White;
            }

            FixCalendarColors(child);
        }
    }
}
