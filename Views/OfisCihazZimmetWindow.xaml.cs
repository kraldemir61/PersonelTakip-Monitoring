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
    public partial class OfisCihazZimmetWindow : Window
    {
        private readonly OfisCihazi _cihaz;
        private readonly bool _isIade;
        private readonly DatabaseService _databaseService;
        private List<Personel> _tumPersoneller = new();
        private ICollectionView? _personelView;

        public OfisCihazZimmetWindow(OfisCihazi cihaz, bool isIade)
        {
            InitializeComponent();
            _cihaz = cihaz;
            _isIade = isIade;
            _databaseService = new DatabaseService();

            TxtTitle.Text = _isIade ? "Ofis Cihazı İade Al" : "Ofis Cihazı Zimmetle";
            TxtCihaz.Text = $"{_cihaz.CihazAdi} - {_cihaz.Marka} {_cihaz.Model} ({_cihaz.SeriNo})";
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
                var liste = await _databaseService.PersonelleriGetirAsync();
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
                    await _databaseService.OfisCihaziIadeAlAsync(_cihaz.Id, tarih, aciklama);
                }
                else
                {
                    if (LstPersonel.SelectedValue == null)
                    {
                        MessageBox.Show("Personel seçiniz.");
                        return;
                    }
                    await _databaseService.OfisCihaziZimmetleAsync(_cihaz.Id, (Guid)LstPersonel.SelectedValue, tarih, aciklama);
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
    }
}
