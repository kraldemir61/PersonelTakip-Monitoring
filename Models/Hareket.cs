namespace PersonelTakip.Monitoring.Models;

public class Hareket
{
    public Guid Id { get; set; }
    public Guid HPersonelId { get; set; }
    public DateTime Tarih { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public string? AdiSoyadi { get; set; }
}
