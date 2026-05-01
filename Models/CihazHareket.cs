using System;

namespace PersonelTakip.Monitoring.Models
{
    public class CihazHareket
    {
        public Guid Id { get; set; }
        public Guid CihazId { get; set; }
        public Guid? NeredenSantiyeId { get; set; }
        public Guid? NereyeSantiyeId { get; set; }
        public DateTime Tarih { get; set; }
        public Guid KullaniciId { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public string IslemTuru { get; set; } = string.Empty; // Transfer, Arıza, Bakım, Kalibrasyon, Giriş, Zimmet, İade
        public CihazTuru CihazTuru { get; set; }
        public Guid? PersonelId { get; set; }
        public string? PersonelAd { get; set; }

        // Display Properties
        public string? CihazSeriNo { get; set; }
        public string? CihazAdi { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazModel { get; set; }
        public string? CihazOzellik { get; set; }
        public string? CihazNot { get; set; }
        public string? CihazBilgi { get; set; }
        public string? NeredenSantiyeAdi { get; set; }
        public string? NereyeSantiyeAdi { get; set; }
        public string? NeredenSantiyeKod { get; set; }
        public string? NereyeSantiyeKod { get; set; }
        public string? KullaniciAdi { get; set; }
    }
}
