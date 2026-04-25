# Düzenleme Notları

Bu dosya, kullanıcı talimatları ve yapılan işlemlerin kronolojik kaydını tutar.

---

### [25.04.2026 15:00] - Veritabanı Temizleme ve Başlangıç
- **Talimat**: Süper admin bilgileri ve tanımlamalar hariç veritabanını temizle.
- **İşlem**: `DatabaseService`'e `ExecuteSqlAsync` eklendi. `MainViewModel`'e `ResetDatabaseAsync` komutu eklendi. `MainWindow` "Toplu İşlemler" sekmesine kırmızı "Veritabanını Temizle" butonu eklendi.

### [25.04.2026 15:10] - Hata Düzeltmeleri
- **İşlem**: `MainWindow`'daki `ScrollViewer` çocuk öğe hatası düzeltildi (StackPanel içine alındı).
- **İşlem**: `MainViewModel`'deki yanlış metod ismi (`LoadDataAsync` -> `LoadAllDataAsync`) düzeltildi.

### [25.04.2026 15:15] - Ölçüm Cihazları Yetkilendirme ve Görünürlük
- **Talimat**: Kullanıcılar boştaki cihazları çekebilmeli, sadece boştaki ve kendi şantiyesindeki cihazları görmeli.
- **İşlem**: `FilterOlcumCihazlari` güncellendi (Normal kullanıcılar için: Idle + MySantiye).
- **İşlem**: `DuzenleOlcumCihaziAsync` ve `SilOlcumCihaziAsync` komutlarındaki boştaki cihaz kısıtlaması (hata mesajı) giderildi.
- **İşlem**: `CihazHareketViewModel`'de güvenlik kontrolleri sıkılaştırıldı (Şantiyeler arası transfer engellendi).

### [25.04.2026 15:18] - Rol Bazlı Erişim ve Silme Yetkisi
- **Talimat**: Süper Admin ve Atanmış Admin her şeyi görür. Normal kullanıcılar sadece boştakileri ve kendininkileri görür. Silme sadece Süper Admin.
- **İşlem**: Görünürlük filtrelerinde `IsSuperAdmin` yerine `IsAdmin` kontrolü kullanıldı (Adminlerin her şeyi görmesi sağlandı).
- **İşlem**: Ölçüm cihazı silme yetkisi sadece `IsSuperAdmin` ("Admin" hesabı) ile sınırlandırıldı.

### [25.04.2026 15:20] - Ofis Cihazları Geri Alımı
- **Talimat**: Ofis cihazları için bekle, acele etme.
- **İşlem**: Ofis cihazları üzerinde yapılan yetki kısıtlamaları geri alındı, orijinal haline döndürüldü.

### [25.04.2026 15:25] - Arayüz Düzenlemeleri
- **Talimat**: Cihaz silme yetkisi olmayanlar sil butonunu görmesin.
- **İşlem**: `MainWindow`'daki ölçüm cihazları "Sil" butonunun `Visibility` özelliği `IsSuperAdmin` ve `BoolToVisibilityConverter` ile bağlandı.

### [25.04.2026 15:28] - ComboBox Filtreleme Hatası Düzeltmesi
- **Talimat**: Cihaz arandığı zaman şantiyeler combobox'ta görünmüyor.
- **İşlem**: `MainViewModel`'deki tüm `ICollectionView` oluşturma işlemleri `GetDefaultView` yerine `new ListCollectionView` olarak değiştirildi. Bu sayede ana ekrandaki arama filtresinin düzenleme pencerelerindeki açılır listeleri etkilemesi engellendi.

### [25.04.2026 15:32] - Sahip Firma Kısıtlaması
- **Talimat**: Ölçüm cihazlarındaki sahip firma bilgisini sadece süper admin değiştirebilir.
- **İşlem**: `CihazEditViewModel`'e `IsSuperAdmin` özelliği eklendi. `CihazEditWindow`'da "Sahip Firma" ComboBox'ının `IsEnabled` özelliği bu veriye bağlandı.

### [25.04.2026 15:45] - Personel Toplu İşlem Geliştirmeleri
- **Talimat**: Ölçüm cihazlarındaki akıllı tanımlama (otomatik ekleme) mantığını personeller için de uygula.
- **İşlem**: `ExcelService.ExceldenOkuAsync` metodu güncellendi, Excel'deki ham metinler `Display` özelliklerine (BolumuDisplay vb.) atandı.
- **İşlem**: `MainViewModel`'e `EnsurePersonelLookupsAsync` metodu eklendi.
- **İşlem**: `CheckAndAddLookupAsync` metodu ID dönecek şekilde güncellendi.
- **İşlem**: `ExcelTopluYukleAsync` ve `ExcelTopluGuncelleAsync` komutları, eksik tanımlamaları otomatik oluşturacak şekilde güncellendi.
