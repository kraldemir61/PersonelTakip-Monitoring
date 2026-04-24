using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace PersonelTakip.Views;

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
}
