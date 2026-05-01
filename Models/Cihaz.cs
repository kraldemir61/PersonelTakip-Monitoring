using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonelTakip.Monitoring.Models
{
    public enum CihazTuru
    {
        Olcum = 0,
        Ofis = 1
    }

    public class Cihaz : INotifyPropertyChanged
    {
        private Guid _id;
        private string? _seriNo;
        private string? _cihazAdi;
        private string? _marka;
        private string? _model;
        private string? _ozellik;
        private string? _sahipFirma;
        private string? _not;
        private CihazTuru _tur;
        private Guid? _santiyeId;
        private string? _durum; // Boşta, Şantiyede, Arızalı, Bakımda, Kalibrasyonda
        private DateTime _sonIslemTarihi;
        
        // Yeni Zimmet ve Fotoğraf Alanları
        private string? _fotoPath;
        private Guid? _zimmetliPersonelId;
        private string? _zimmetliPersonelAd;
        private DateTime? _zimmetTarihi;

        // Display Properties for UI
        public string? SantiyeAdi { get; set; }
        public string? SantiyeKod { get; set; }

        public Guid Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string? SeriNo
        {
            get => _seriNo;
            set { _seriNo = value; OnPropertyChanged(); }
        }

        public string? CihazAdi
        {
            get => _cihazAdi;
            set { _cihazAdi = value; OnPropertyChanged(); }
        }

        public string? Marka
        {
            get => _marka;
            set { _marka = value; OnPropertyChanged(); }
        }

        public string? Model
        {
            get => _model;
            set { _model = value; OnPropertyChanged(); }
        }
        
        public string? Ozellik
        {
            get => _ozellik;
            set { _ozellik = value; OnPropertyChanged(); }
        }

        public string? SahipFirma
        {
            get => _sahipFirma;
            set { _sahipFirma = value; OnPropertyChanged(); }
        }

        public string? Not
        {
            get => _not;
            set { _not = value; OnPropertyChanged(); }
        }

        public CihazTuru Tur
        {
            get => _tur;
            set { _tur = value; OnPropertyChanged(); }
        }

        public Guid? SantiyeId
        {
            get => _santiyeId;
            set { _santiyeId = value; OnPropertyChanged(); }
        }

        public string? Durum
        {
            get => _durum;
            set { _durum = value; OnPropertyChanged(); }
        }

        public DateTime SonIslemTarihi
        {
            get => _sonIslemTarihi;
            set { _sonIslemTarihi = value; OnPropertyChanged(); }
        }

        // Yeni Property Implementation
        public string? FotoPath
        {
            get => _fotoPath;
            set 
            { 
                _fotoPath = value; 
                _currentFotoIndex = 0;
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(Fotograflar));
                OnPropertyChanged(nameof(CurrentFoto));
                OnPropertyChanged(nameof(IsFirstFoto));
                OnPropertyChanged(nameof(IsLastFoto));
            }
        }

        public Guid? ZimmetliPersonelId
        {
            get => _zimmetliPersonelId;
            set { _zimmetliPersonelId = value; OnPropertyChanged(); OnPropertyChanged(nameof(ZimmetliMi)); }
        }

        public string? ZimmetliPersonelAd
        {
            get => _zimmetliPersonelAd;
            set { _zimmetliPersonelAd = value; OnPropertyChanged(); }
        }

        public DateTime? ZimmetTarihi
        {
            get => _zimmetTarihi;
            set { _zimmetTarihi = value; OnPropertyChanged(); }
        }

        public bool ZimmetliMi => ZimmetliPersonelId.HasValue;

        // Fotoğraf Navigasyon Yardımcıları
        public System.Collections.Generic.List<string> Fotograflar => 
            string.IsNullOrEmpty(FotoPath) ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string>(FotoPath.Split('|', StringSplitOptions.RemoveEmptyEntries));

        private int _currentFotoIndex = 0;
        public int CurrentFotoIndex
        {
            get => _currentFotoIndex;
            set
            {
                _currentFotoIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFoto));
                OnPropertyChanged(nameof(IsFirstFoto));
                OnPropertyChanged(nameof(IsLastFoto));
            }
        }

        public string? CurrentFoto => Fotograflar.Count > 0 && CurrentFotoIndex >= 0 && CurrentFotoIndex < Fotograflar.Count ? Fotograflar[CurrentFotoIndex] : null;
        public bool IsFirstFoto => CurrentFotoIndex <= 0;
        public bool IsLastFoto => CurrentFotoIndex >= Fotograflar.Count - 1;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
