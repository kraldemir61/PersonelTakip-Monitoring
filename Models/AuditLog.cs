namespace PersonelTakip.Monitoring.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? KullaniciId { get; set; }
    public string? KullaniciAdi { get; set; }
    public string TabloAdi { get; set; } = string.Empty;
    public string? KayitId { get; set; }
    public string IslemTipi { get; set; } = string.Empty;
    public string? EskiDeger { get; set; }
    public string? YeniDeger { get; set; }
    public string? IpAdresi { get; set; }
    public string? Aciklama { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
}
