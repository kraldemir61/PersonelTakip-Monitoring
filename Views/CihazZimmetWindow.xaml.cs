using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PersonelTakip.Monitoring.Helpers;
using PersonelTakip.Monitoring.Models;
using PersonelTakip.Monitoring.Services;

namespace PersonelTakip.Monitoring.Views
{
    public partial class CihazZimmetWindow : Window
    {
        private readonly Cihaz _cihaz;
        private readonly bool _isIade;
        private readonly DatabaseService _databaseService;
        private readonly Kullanici _currentUser;
        private readonly bool _isAdmin;
        private List<Personel> _tumPersoneller = new();
        private ICollectionView? _personelView;

        public CihazZimmetWindow(Cihaz cihaz, bool isIade, Kullanici currentUser, bool isAdmin)
        {
            InitializeComponent();
            _cihaz = cihaz;
            _isIade = isIade;
            _currentUser = currentUser;
            _isAdmin = isAdmin;
            _databaseService = new DatabaseService();

            TxtTitle.Text = _isIade ? "Cihaz İade Al" : "Cihaz Zimmetle";
            TxtCihaz.Text = $"{_cihaz.CihazAdi} ({_cihaz.SeriNo})";
            DtTarih.SelectedDate = DateTime.Now;

            if (_isIade)
            {
                PnlPersonel.Visibility = Visibility.Collapsed;
                Height = 350;
            }
            else
            {
                LoadPersonel();
            }
        }

        private async void LoadPersonel()
        {
            try
            {
                var santiyeId = _isAdmin ? null : _currentUser?.SantiyeId;
                var liste = await _databaseService.PersonelleriGetirAsync(santiyeId);
                _tumPersoneller = liste.Where(p => p.Aktif).ToList();
                LstPersonel.ItemsSource = _tumPersoneller;
                _personelView = CollectionViewSource.GetDefaultView(LstPersonel.ItemsSource);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Personel yüklenirken hata: {ex.Message}");
            }
        }

        private void TxtPersonelAra_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_personelView == null) return;
            var txt = TxtPersonelAra.Text;
            _personelView.Filter = string.IsNullOrWhiteSpace(txt) ? null : (o => 
            {
                var p = (Personel)o;
                return StringHelper.SmartSearch($"{p.AdiSoyadi} {p.BolumuDisplay}", txt);
            });
        }

        private async void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tarih = DtTarih.SelectedDate ?? DateTime.Now;
                var aciklama = TxtAciklama.Text;

                if (_isIade)
                {
                    // Ölçüm cihazları için DatabaseService içinde IadeAlAsync metodunu kullanıyoruz
                    await _databaseService.IadeAlAsync(_cihaz.Id, tarih, aciklama, _currentUser.Id);
                }
                else
                {
                    if (LstPersonel.SelectedValue == null)
                    {
                        MessageBox.Show("Personel seçiniz.");
                        return;
                    }
                    // Ölçüm cihazları için DatabaseService içinde ZimmetleAsync metodunu kullanıyoruz
                    await _databaseService.ZimmetleAsync(_cihaz.Id, (Guid)LstPersonel.SelectedValue, tarih, aciklama, _currentUser.Id);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}");
            }
        }

        private void LstPersonel_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (LstPersonel.SelectedItem != null) BtnKaydet_Click(sender, e);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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
                if (child is TextBlock tb) tb.Foreground = System.Windows.Media.Brushes.White;
                else if (child is System.Windows.Controls.Primitives.CalendarDayButton dayBtn) dayBtn.Foreground = System.Windows.Media.Brushes.White;
                else if (child is System.Windows.Controls.Primitives.CalendarButton calBtn) calBtn.Foreground = System.Windows.Media.Brushes.White;
                FixCalendarColors(child);
            }
        }
    }
}
