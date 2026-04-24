# Personel Takip WPF Projesi

Bu proje, WPF ve MVVM mimarisi kullanılarak geliştirilmiş bir Personel Takip uygulamasıdır.

## Güncelleme Notları (Son Push)
- **Güvenlik Yedeklemesi:** Yeni işlemlere başlamadan önce mevcut temiz durum commit'lenip pushlandı.
- **Login Focus:** Giriş ekranı açıldığında şifre giriş kutusuna otomatik odaklanma (Focus) özelliği eklendi.
- **Excel Aktarımı:** `ClosedXML` paketi entegre edildi. Ana ekrana eklenen 'Excel'e Aktar' butonuyla personel listesinin biçimlendirilmiş bir tablo olarak (*.xlsx formatında) dışa aktarılması sağlandı. Süper Admin olmayanların maaş bilgileri gizlenerek kaydedildi.
- **Modern Dashboard:** Seçili personel olmadığında gösterilen boş detay paneli, Şantiye, Bölüm ve Uyruk dağılımını gösteren modern bir istatistik kartına dönüştürüldü.
