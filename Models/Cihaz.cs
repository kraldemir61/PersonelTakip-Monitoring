using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonelTakip.Models
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
        private string? _sahipFirma;
        private string? _not;
        private CihazTuru _tur;
        private Guid? _santiyeId;
        private string? _durum; // Boşta, Şantiyede, Arızalı, Bakımda, Kalibrasyonda
        private DateTime _sonIslemTarihi;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
