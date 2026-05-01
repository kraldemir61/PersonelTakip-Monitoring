using System.Windows;
using System.Windows.Controls;

namespace PersonelTakip.Monitoring.Helpers
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper), new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box || (bool)d.GetValue(UpdatingPasswordProperty))
            {
                return;
            }

            box.Password = e.NewValue as string ?? string.Empty;
        }

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box) return;

            if (e.NewValue is bool bind && bind)
            {
                box.PasswordChanged += HandlePasswordChanged;
            }
            else
            {
                box.PasswordChanged -= HandlePasswordChanged;
            }
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox box) return;
            
            SetUpdatingPassword(box, true);
            SetBoundPassword(box, box.Password);
            SetUpdatingPassword(box, false);
        }

        public static void SetBindPassword(DependencyObject dp, bool value) => dp?.SetValue(BindPasswordProperty, value);
        public static bool GetBindPassword(DependencyObject dp) => dp != null && (bool)dp.GetValue(BindPasswordProperty);

        public static string GetBoundPassword(DependencyObject dp) => dp?.GetValue(BoundPasswordProperty) as string ?? string.Empty;
        public static void SetBoundPassword(DependencyObject dp, string value) => dp?.SetValue(BoundPasswordProperty, value);

        private static void SetUpdatingPassword(DependencyObject dp, bool value)
        {
            if (dp == null) return;
            dp.SetValue(UpdatingPasswordProperty, value);
        }
    }
}
