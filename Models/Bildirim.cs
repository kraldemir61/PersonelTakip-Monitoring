using System;

namespace PersonelTakip.Monitoring.Models;

public class Bildirim
{
    public int Id { get; set; }
    public string Mesaj { get; set; } = string.Empty;
    public Guid TetikleyenKullaniciId { get; set; }
    public DateTime Tarih { get; set; }
    public bool OkunduMu { get; set; }
    public Guid? HedefSantiyeId { get; set; }
}
