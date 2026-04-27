using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PersonelTakip.Helpers;
using PersonelTakip.Models;
using PersonelTakip.Services;

namespace PersonelTakip.Views
{
    public partial class CihazZimmetWindow : Window
    {
        private readonly Cihaz _cihaz;
        private readonly bool _isIade;
        private readonly DatabaseService _databaseService;
        private List<Personel> _tumPersoneller = new();
        private ICollectionView _personelView = null!;

        public string TitleText => _isIade ? "Cihaz İade Al" : "Cihaz Zimmetle";

        public CihazZimmetWindow(Cihaz cihaz, bool isIade)
        {
            InitializeComponent();
            _cihaz = cihaz;
            _isIade = isIade;
            _databaseService = new DatabaseService();

            TxtTitle.Text = TitleText;
            TxtCihaz.Text = $"{_cihaz.CihazAdi} - {_cihaz.Marka} {_cihaz.Model} ({_cihaz.SeriNo})";
            DtTarih.SelectedDate = DateTime.Now;

            LoadUI();
        }

        private void LoadUI()
        {
            if (_isIade)
            {
                PnlPersonel.Visibility = Visibility.Collapsed;
                Height = 300;
            }
            else
            {
                PnlPersonel.Visibility = Visibility.Visible;
                LoadPersonel();
            }
        }

        private async void LoadPersonel()
        {
            try
            {
                var liste = await _databaseService.PersonelleriGetirAsync();
                _tumPersoneller = liste.Where(p => p.Aktif).ToList();
                LstPersonel.ItemsSource = _tumPersoneller;
                _personelView = CollectionViewSource.GetDefaultView(LstPersonel.ItemsSource);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Personel listesi yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtPersonelAra_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_personelView == null) return;

            var aramaMetni = TxtPersonelAra.Text;

            if (string.IsNullOrWhiteSpace(aramaMetni))
            {
                _personelView.Filter = null;
            }
            else
            {
                _personelView.Filter = o =>
                {
                    if (o is not Personel p) return false;
                    var personelMetni = $"{p.AdiSoyadi} {p.BolumuDisplay} {p.GoreviDisplay}";
                    return StringHelper.SmartSearch(personelMetni, aramaMetni);
                };
            }
        }

        private async void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tarih = DtTarih.SelectedDate ?? DateTime.Today;
                tarih = tarih.Date.Add(DateTime.Now.TimeOfDay);
                var aciklama = TxtAciklama.Text;

                if (_isIade)
                {
                    await _databaseService.IadeAlAsync(_cihaz.Id, tarih, aciklama);
                }
                else
                {
                    if (LstPersonel.SelectedValue == null)
                    {
                        MessageBox.Show("Lütfen bir personel seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    Guid personelId = (Guid)LstPersonel.SelectedValue;
                    await _databaseService.ZimmetleAsync(_cihaz.Id, personelId, tarih, aciklama);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İşlem hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LstPersonel_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (LstPersonel.SelectedItem != null)
            {
                BtnKaydet_Click(sender, e);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
