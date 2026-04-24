namespace PersonelTakip.Models;

public class Santiye
{
    public Guid Id { get; set; }
    public string Adi { get; set; } = string.Empty;
    public string Kod { get; set; } = string.Empty;
    public string? Adres { get; set; }

    public bool Aktif { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
