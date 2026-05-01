using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PersonelTakip.Monitoring.Converters;

public class InverseBoolToFontWeightConverter : IValueConverter
{
    public object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Translate(object value)
    {
        if (value is bool boolValue)
        {
            return boolValue ? FontWeights.Normal : FontWeights.Bold;
        }
        return FontWeights.Normal;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Translate(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
