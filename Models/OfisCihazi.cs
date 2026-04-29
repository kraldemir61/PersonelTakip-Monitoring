using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonelTakip.Models
{
    public class OfisCihazi : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private Guid _id;
        private string? _seriNo;
        private string? _cihazAdi;
        private string? _marka;
        private string? _model;
        private string? _ozellik;
        private string? _not;
        private string? _durum;
        private DateTime _sonIslemTarihi;
        private string? _fotoPath;
        private Guid? _zimmetliPersonelId;
        private string? _zimmetliPersonelAd;
        private DateTime? _zimmetTarihi;

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

        public string? Not
        {
            get => _not;
            set { _not = value; OnPropertyChanged(); }
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
            }
        }

        public List<string> Fotograflar => 
            string.IsNullOrEmpty(FotoPath) ? new List<string>() : new List<string>(FotoPath.Split('|', StringSplitOptions.RemoveEmptyEntries));

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
        
        public bool IsFirstFoto => CurrentFotoIndex <= 0;
        public bool IsLastFoto => CurrentFotoIndex >= Fotograflar.Count - 1;

        public string? CurrentFoto => Fotograflar.Count > 0 && CurrentFotoIndex >= 0 && CurrentFotoIndex < Fotograflar.Count ? Fotograflar[CurrentFotoIndex] : null;

        public Guid? ZimmetliPersonelId
        {
            get => _zimmetliPersonelId;
            set { _zimmetliPersonelId = value; OnPropertyChanged(); }
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

        private string? _bulunduguSantiyeKod;
        public string? BulunduguSantiyeKod
        {
            get => _bulunduguSantiyeKod;
            set { _bulunduguSantiyeKod = value; OnPropertyChanged(); }
        }

        private Guid? _santiyeId;
        public Guid? SantiyeId
        {
            get => _santiyeId;
            set { _santiyeId = value; OnPropertyChanged(); }
        }

        // Ofis cihazları için Tur her zaman Ofis (1)
        public int Tur => 1;

        public bool ZimmetliMi => ZimmetliPersonelId.HasValue;
    }
}
