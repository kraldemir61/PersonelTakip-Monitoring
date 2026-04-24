namespace PersonelTakip.Models;

public class Personel
{
    public Guid Id { get; set; }
    public Guid? SantiyeId { get; set; }
    public string AdiSoyadi { get; set; } = string.Empty;
    public int? SantiyeAdi { get; set; }
    public int? Bolumu { get; set; }
    public int? Gorevi { get; set; }
    public int? Uyrugu { get; set; }
    public DateTime? IseGirisTarihi { get; set; }
    public string? TelefonNumarasi { get; set; }
    public decimal? Maas { get; set; }
    public int? ParaBirimi { get; set; }
    public bool Aktif { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? SantiyeAdiDisplay { get; set; }
    public string? SantiyeKod { get; set; }
    public string? BolumuDisplay { get; set; }
    public string? GoreviDisplay { get; set; }
    public string? UyruguDisplay { get; set; }
    public string? ParaBirimiDisplay { get; set; }
    
    public bool IsSystemUser { get; set; } = false;
}