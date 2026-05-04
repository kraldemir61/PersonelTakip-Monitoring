using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonelTakip.Monitoring.Models;

public class Kullanici : INotifyPropertyChanged
{
    public Guid Id { get; set; }
    public string KullaniciAdi { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SifreHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "User";
    public Guid? SantiyeId { get; set; }
    public bool Aktif { get; set; } = true;
    public DateTime? SonGiris { get; set; }

    private DateTime? _sonHareket;
    public DateTime? SonHareket
    {
        get => _sonHareket;
        set
        {
            if (_sonHareket != value)
            {
                _sonHareket = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOnline));
            }
        }
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? SantiyeAdi { get; set; }
    public string? SantiyeKod { get; set; }

    public bool IsOnline
    {
        get
        {
            if (!SonHareket.HasValue) return false;
            
            DateTime sonHareketUtc;
            if (SonHareket.Value.Kind == DateTimeKind.Unspecified)
                sonHareketUtc = DateTime.SpecifyKind(SonHareket.Value, DateTimeKind.Utc);
            else
                sonHareketUtc = SonHareket.Value.ToUniversalTime();

            var fark = DateTime.UtcNow - sonHareketUtc;
            
            // Saat farklarına karşı toleranslı kontrol:
            // 1. Gelecek zamanlı ise (sinyali gönderen bilgisayarın saati ilerideyse) 1 saate kadar online kabul et
            // 2. Geçmiş zamanlı ise (sinyal gecikmişse) 10 dakikaya kadar online kabul et
            return fark.TotalMinutes < 10 && fark.TotalMinutes > -60;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}