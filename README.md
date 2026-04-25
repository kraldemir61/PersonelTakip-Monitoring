# Personel Takip WPF Projesi

Bu proje, WPF ve MVVM mimarisi kullanılarak geliştirilmiş bir Personel Takip uygulamasıdır.

- **Personel Listesi Düzenlemesi:** Sistem kullanıcılarının (Admin/User) personel listesinde otomatik görünmesi özelliği iptal edildi. Kullanıcılar isterlerse kendilerini manuel olarak personel olarak ekleyebilirler.
- **Kullanıcı Sınırı:** Her şantiye için aktif kullanıcı sayısı maksimum 2 (Admin + User) olarak sınırlandırıldı.
- **Validasyon Kuralları:** Personel eklerken Uyruk ve diğer temel alanlar zorunlu hale getirildi. Maaş girilmişse para birimi seçimi zorunlu kılındı.
- **Bildirim Merkezi Geliştirmeleri:** Bildirimleri tek tek silme ve tümünü temizleme özellikleri eklendi. Anlık bildirim iletimi optimize edildi.
- **Pencere Düzenlemeleri:** Dialog pencereleri için yeniden boyutlandırma (Resize) kapatıldı ve `CustomTitleBar` üzerinden maximize butonu gizlendi.
- **Navigasyon İyileştirmesi:** 'Çıkış' butonu daha erişilebilir olması için sekme alanının en sağına taşındı. Sol menü (Sidebar) daha kompakt bir görünüm için daraltıldı.
- **Bildirim Merkezi:** Bildirim penceresinin açılış konumu zil ikonuyla tam hizalanacak şekilde (sol hizalı) optimize edildi.
- **Görsel İyileştirmeler:** Butonlardaki metin kırpılma sorunları giderildi ve tüm pencerelerde görsel tutarlılık sağlandı.
- **Güvenlik Yedeklemesi:** Mevcut temiz durum commit'lenip pushlandı.
- **Login Focus:** Giriş ekranı açıldığında şifre giriş kutusuna otomatik odaklanma (Focus) özelliği eklendi.
- **Excel Aktarımı:** `ClosedXML` paketi entegre edildi. Ana ekrana eklenen 'Excel'e Aktar' butonuyla personel listesinin biçimlendirilmiş bir tablo olarak (*.xlsx formatında) dışa aktarılması sağlandı.
- **Modern Dashboard:** Seçili personel olmadığında gösterilen boş detay paneli, modern istatistik kartlarına dönüştürüldü.
