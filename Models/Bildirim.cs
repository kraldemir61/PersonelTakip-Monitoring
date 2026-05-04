using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PersonelTakip.Monitoring.Models;

public partial class Bildirim : ObservableObject
{
    public int Id { get; set; }
    public string Mesaj { get; set; } = string.Empty;
    public Guid TetikleyenKullaniciId { get; set; }
    public DateTime Tarih { get; set; }

    [ObservableProperty]
    private bool _okunduMu;

    public Guid? HedefSantiyeId { get; set; }
}
