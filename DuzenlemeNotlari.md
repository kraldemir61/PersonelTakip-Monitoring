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

### [25.04.2026 16:01] - Git Push
- **Talimat**: Projeyi push et.
- **İşlem**: `DuzenlemeNotlari.md` repo içine taşındı. Visual Studio'ya gömülü Git kullanılarak tüm değişiklikler GitHub'a (`origin master`) başarıyla push edildi.

### [25.04.2026 16:04] - Excel Dosya Adı Formatı
- **Talimat**: İndirilen excel dosyalarında "25.04.2026 16.03.22" formatını kullan.
- **İşlem**: `ExcelService` içindeki tüm dışarı aktarma (Personel, Cihaz, Hareketler) dosya adı şablonları saniye detaylı tarih formatına güncellendi.

### [25.04.2026 16:13] - Personel Toplu Yükleme Hatası Düzeltilmesi
- **Hata**: Bölüm sütunundaki verilerin bazen boş kaydedilmesi.
- **İşlem**: `ExcelService.ExceldenOkuAsync` metotunda tüm hücre verilerine `.Trim()` eklendi (gizli boşluk sorunları için).
- **İşlem**: `DatabaseService.LookupOlusturAsync` metodunda `SELECT lastval()` yerine daha güvenilir olan `RETURNING id` yapısına geçildi.
- **İşlem**: Proje tekrar push edildi.

### [25.04.2026 16:34] - Personel Kayıt Esnekliği
- **Talimat**: Yeni personel kayıtta telefon zorunlu alan olmayacak.
- **İşlem**: `PersonelEditViewModel.cs` içindeki telefon numarası zorunluluk kontrolü kaldırıldı.

### [25.04.2026 16:45] - Cihaz Yönetimi Arayüz Modernizasyonu
- **Talimat**: Cihazları da personeller gibi layout tasarımında yapalım.
- **İşlem**: "Ölçüm Cihazları" ve "Ofis Cihazları" sekmeleri, Personel sekmesiyle aynı 3 sütunlu yapıya (Liste + Detay Kartı) dönüştürüldü.
- **İşlem**: Cihazlar için sağ tarafta dinamik detay kartı ve cihaz seçili değilken görünen "Genel Durum Özeti" eklendi.
- **İşlem**: `MainViewModel` üzerinde cihaz istatistiklerini hesaplayan `CalculateDeviceStats` mantığı kuruldu ve arama ile entegre edildi.

### [27.04.2026 08:15] - Gerçek Zamanlı Cihaz Transfer Senkronizasyonu
- **Talimat**: Kullanıcının yapmış olduğu transfer, anında transferi alan kullanıcının ve adminlerin ekranına yansımalı. Programı kapatıp açmak gerekmemeli.
- **İşlem**: `MainViewModel.cs` içindeki PostgreSQL `LISTEN/NOTIFY` bildirim dinleyicisi (`StartNotificationListener`) güncellendi. Artık bir transfer veya sistem bildirimi alındığında, uygulamanın cihaz verileri (`LoadCihazlarAsync`) ve istatistikleri arkaplanda anında yeniden yükleniyor ve ekranlara (OlcumCihazlariView, OfisCihazlariView) anlık olarak yansıtılıyor.

### [27.04.2026 08:31] - Personel Silme Bildirimi ve Senkronizasyonu
- **Talimat**: Personel silindiğinde bildirim gitmiyor, düzeltilmeli. Eklenen/Silinen personeller anında admin ekranına yansımalı. Süper admin tanımlaması bulunmadığından metin değiştirilmeli.
- **İşlem**: `MainViewModel.cs` içindeki `SilPersonelAsync` ve `PersonelEditViewModel` içerisine daha akıcı bir bildirim eklendi (Örn: "Ali Yılmaz, Admin tarafından BWC şantiyesinden çıkartıldı.").
- **İşlem**: `StartNotificationListener` içindeki yenileme metoduna `LoadPersonellerAsync()` eklendi. Böylece bir personel eklendiğinde veya silindiğinde, bildirim anında tüm açık ekranlarda personel tablosu da otomatik olarak güncellenecek.

### [27.04.2026 08:45] - Admin Yetkisi Bildirimi ve Otomatik Kapanma
- **Talimat**: Süper admin, bir Kullanıcıya adminlik verdiğinde veya adminlikten çıkardığında ilgili kullanıcıya bildirim gitmeli ve programı yeniden başlatması istenmeli.
- **İşlem**: `DatabaseService.cs` içerisine `SistemBildirimiGonderAsync` eklendi. Bu yapı sayesinde veritabanına kayıt atılmadan, sadece LISTEN/NOTIFY kanalı üzerinden özel sistem komutları gönderilebilmesi sağlandı.
- **İşlem**: `KullaniciEditViewModel.cs` güncellendi. Kullanıcının orijinal yetkisi değişirse (Admin <-> User), bu kullanıcıya özel `RESTART_TARGET` sistem sinyali yollanıyor ve sisteme "Yetkisi ... olarak güncellendi" tarzı global bir bildirim ekleniyor.

### [27.04.2026 09:07] - Cihaz Ekle/Düzenle Yetkilendirmesi
- **Talimat**: Kullanıcılar, Ölçüm Cihazları sekmesinde "Cihaz Ekle" ve "Düzenle" butonlarını göremesin.
- **İşlem**: `MainWindow.xaml` içerisindeki "Ölçüm Cihazları" sekmesinde bulunan "Cihaz Ekle" ve "Düzenle" butonlarına `Visibility="{Binding IsAdmin, Converter={StaticResource BoolToVisibilityConverter}}"` eklendi. Sadece Admin yetkisine sahip hesaplar bu butonları görebilecek. Normal kullanıcılar sadece cihaz transferi yapabilecek ve geçmişini görüntüleyebilecek.
- **İşlem**: `MainViewModel.cs` içindeki dinleyici, gelen sinyalin kendi ID'sine ait bir RESTART komutu olduğunu tespit ederse, doğrudan ekrana bir uyarı (MessageBox) çıkartıyor ve onaylandığında `Application.Current.Shutdown()` ile programı güvenli şekilde sonlandırıyor.

### [27.04.2026 09:41] - Kullanıcı Bazlı Bildirim Yönetimi
- **Talimat**: Kullanıcılar veya adminler bildirimleri silince diğer kullanıcılardan silinmesin. Bildirim yönetimi her kullanıcıya özel olmalı.
- **İşlem**: `bildirim_durumlari` adında yeni bir tablo oluşturuldu. Bu tablo, her bildirimin hangi kullanıcı tarafından okunduğunu (`okundu_mu`) ve silindiğini (`silindi_mi`) takip eder.
- **İşlem**: `DatabaseService.cs` içerisindeki bildirim çekme, okundu yapma ve silme metodları `kullanici_id` parametresi alacak şekilde güncellendi. Artık bir bildirim silindiğinde veritabanından tamamen kaldırılmıyor, sadece o kullanıcı için "silindi" olarak işaretleniyor.
- **İşlem**: `MainViewModel.cs` üzerinde bildirimlerle ilgili tüm işlemler (yükleme, popup açma, tekli/toplu silme) mevcut kullanıcının ID'sini veritabanına gönderecek şekilde revize edildi.
