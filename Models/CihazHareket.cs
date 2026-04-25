using System;

namespace PersonelTakip.Models
{
    public class CihazHareket
    {
        public Guid Id { get; set; }
        public Guid CihazId { get; set; }
        public Guid? NeredenSantiyeId { get; set; }
        public Guid? NereyeSantiyeId { get; set; }
        public DateTime Tarih { get; set; }
        public Guid KullaniciId { get; set; }
        public string Aciklama { get; set; }
        public string IslemTuru { get; set; } // Transfer, Arıza, Bakım, Kalibrasyon, Giriş

        // Display Properties
        public string? CihazSeriNo { get; set; }
        public string? CihazAdi { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazModel { get; set; }
        public string? CihazBilgi { get; set; }
        public string? NeredenSantiyeAdi { get; set; }
        public string? NereyeSantiyeAdi { get; set; }
        public string? NeredenSantiyeKod { get; set; }
        public string? NereyeSantiyeKod { get; set; }
        public string? KullaniciAdi { get; set; }
    }
}
