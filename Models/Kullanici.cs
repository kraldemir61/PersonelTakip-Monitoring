namespace PersonelTakip.Monitoring.Models;

public class Kullanici
{
    public Guid Id { get; set; }
    public string KullaniciAdi { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SifreHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "User";
    public Guid? SantiyeId { get; set; }
    public bool Aktif { get; set; } = true;
    public DateTime? SonGiris { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? SantiyeAdi { get; set; }
    public string? SantiyeKod { get; set; }
}