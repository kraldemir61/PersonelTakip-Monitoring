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

### [27.04.2026 09:52] - Veritabanı Bağlantı Paneli ve Kısayol
- **Talimat**: Programın farklı veritabanı bilgileriyle çalışabilmesi için şık bir bağlantı paneli tasarlanmalı. Erişim sadece "Ctrl+Shift+C" kısayoluyla olmalı.
- **İşlem**: `AppConfiguration.cs` dosyasına ayarları `appsettings.json` dosyasına kalıcı olarak kaydeden `Save()` metodu eklendi.
- **İşlem**: `DatabaseSettingsWindow` (XAML) ve `DatabaseSettingsViewModel` (C#) oluşturuldu. Modern, koyu tema ile uyumlu ve kullanıcı dostu bir arayüz tasarlandı.
- **İşlem**: Panelde Host, Port, Database, Username ve Password alanları eklendi. Ayrıca "Bağlantıyı Test Et" özelliği ile kaydetmeden önce doğruluğu kontrol etme imkanı sağlandı.
- **İşlem**: `MainWindow.xaml` ve `LoginWindow.xaml` içerisine global `Ctrl+Shift+C` kısayolu tanımlandı. Bu kısayol tetiklendiğinde ilgili ViewModel üzerinden ayar penceresi modal olarak açılıyor. Artık hem giriş ekranında hem de ana ekranda bağlantı ayarları değiştirilebilir.

### [27.04.2026 14:33] - Gelişmiş Veritabanı Onarımı ve Hızlı Bildirim Mimarisi
- **Talimat**: Ana veritabanındaki tüm tablo ve sütunlar yedek alınsın, geri yükleme sonrası programı kapatıp açmak gerekmesin, bildirimler anlık gitsin.
- **İşlem (Veritabanı Onarımı)**: `DatabaseService.InitializeDatabaseAsync` metodu "Agresif Onarım" moduna geçirildi. Artık veritabanı yedeği geri yüklenirken hedef tabloda eksik olan tüm sütunlar (okundu_mu, silindi_mi, kullanici_id, tetikleyen_kullanici_id vb.) otomatik olarak tespit edilip ekleniyor. Bu sayede ana veritabanından alınan yedekler, yeni kurulan test veritabanlarına %100 uyumla aktarılabiliyor.
- **İşlem (Dinamik Yenileme)**: `AppConfiguration` sınıfına `ConfigurationChanged` olayı (Event) eklendi. Ayarlar kaydedildiğinde veya veriler geri yüklendiğinde bu olay tetikleniyor.
- **İşlem (Sıfır-Restart)**: `LoginViewModel` ve `MainViewModel` bu olayı dinlemeye başladı. Artık veritabanı bilgileri değiştiği veya yedek yüklendiği an; programı kapatmaya gerek kalmadan tüm listeler (Şantiyeler, Personeller, Cihazlar) saniyeler içinde otomatik olarak yenileniyor.
- **İşlem (Bildirim Hızı)**: Cihaz transfer bildirimlerinin başına işlem yapan kullanıcının kimliği (ID) eklenerek format uyumsuzluğu giderildi. `MainViewModel` içindeki bildirim işleme mantığı optimize edildi; artık bildirim alındığı an "Anlık Mesaj" (Live Notification) saniyesinde ekrana düşüyor.
- **İşlem (Veri Güvenliği)**: `DatabaseService` içindeki SQL onarım blokları geliştirildi. integer olması gereken sütunların otomatik dönüşümü ve tüm yardımcı tabloların (Lookup) öncelikli oluşturulması sağlandı.

### [27.04.2026 17:15] - Ofis Cihazları Modülü ve DataGrid Görsel İyileştirmeleri
- **Talimat**: 'MainWindow'daki 'DataGrid' tasarım özelliklerini (alternatif satır rengi ve seçili satır fontunun mavi olması) Ofis Cihazları sekmesine de uygula.
- **İşlem**: 'Ofis Cihazları' listesi için Model, ViewModel ve View katmanları (Ekle/Düzenle/Zimmet pencereleri) tamamlandı.
- **İşlem**: 'DataGrid' görsel uyumu sağlandı: `AlternatingRowBackground="#F8FAFC"` ve seçili satırda tüm metinlerin mavi (`#2563EB`) olmasını sağlayan `DataTrigger` yapıları `MainWindow.xaml` içerisine entegre edildi.

### [27.04.2026 17:25] - Pencere Boyutlandırma ve Düzen Hatalarının Giderilmesi
- **Talimat**: Pencere küçülürken bileşenler birbirinin üstünü kaplıyor ve belirlenen 'MinWidth' değerleri yok sayılıyor.
- **İşlem**: `MainWindow.xaml.cs` dosyasındaki `WM_GETMINMAXINFO` (Win32 Hook) mesaj yönetimi DPI uyumlu hale getirildi. Artık pencere, belirlenen `980x640` sınırlarının altına küçültülemiyor.
- **İşlem**: 'Çıkış' butonu, başlık çubuğundan alınarak sağ üstteki orijinal konumuna geri döndürüldü.
- **İşlem**: Dar pencerelerde içeriklerin kesilmemesi için ana içerik alanına yatay kaydırma desteği (`ScrollViewer`) eklendi.

### [27.04.2026 17:35] - Git ve Filtreleme Mantığı Güncellemeleri
- **İşlem**: 'Boşta' ve 'Zimmetli' filtre butonlarının aynı anda aktif olmama (exclusive) kuralı eklendi. Aktif olan filtrenin arka plan rengiyle vurgulanması sağlandı.
- **İşlem**: Veri yenileme (`LoadData`) sonrası tabloda seçili olan satırın kaybolması engellendi, seçim durumu korundu.
- **İşlem**: Yapılan tüm değişiklikler Visual Studio Git aracı kullanılarak Türkçe ve detaylı açıklama ile GitHub'a push edildi.

### [27.04.2026 17:45] - Cihaz Detay Kartı ve Görsel Özelliğinin Kaldırılması
- **Talimat**: Cihaz görselleri kartını 'Cihaz Bilgileri' olarak değiştir, görsel ekleme özelliğini kaldır. Başlıkta sadece cihaz adı görünsün, diğer tüm detaylar bilgi kartına taşınsın.
- **İşlem**: 'Ölçüm Cihazları' ve 'Ofis Cihazları' detay kartı başlıklarından Seri No, Marka ve Model bilgileri kaldırıldı; sadece 'Cihaz Adı' bırakıldı.
- **İşlem**: 'Ofis Cihazları' sekmesindeki "CİHAZ GÖRSELLERİ" kartı "CİHAZ BİLGİLERİ" olarak yeniden adlandırıldı.
- **İşlem**: Marka, Model, Seri No ve Özellikler bilgileri bu yeni 'CİHAZ BİLGİLERİ' kartına taşınarak görselleştirildi.
- **İşlem**: `CihazEditWindow` ve `OfisCihazEditWindow` pencerelerinden fotoğraf ekleme/listeleme alanları tamamen kaldırıldı ve pencere boyutları optimize edildi.
- **İşlem**: `CihazEditViewModel` ve `OfisCihazEditViewModel` sınıflarındaki tüm fotoğraf işleme, kopyalama ve komut mantığı temizlendi.


### [28.04.2026 07:55] - Cihaz D�zenleme Penceresi Tasarim G�ncellemesi
- **Talimat**: Cihaz ekleme/d�zenleme pencerelerindeki 6 satirlik dikey yapiyi 4 satira indir.
- **Islem**: OfisCihazEditWindow ve CihazEditWindow pencereleri yeniden tasarlandi:
    - 1. Satir: Cihaz Adi (Tam genislik)
    - 2. Satir: Marka ve Model (Yan yana)
    - 3. Satir: �zellikler ve Seri No (Yan yana)
    - 4. Satir: A�iklama (Tam genislik)
    - (�l��m cihazlari i�in ek olarak Santiye ve Sahip Firma bilgileri 3. satira eklendi).
- **Islem**: Pencerelerin genisligi yan yana yerlesim i�in 500px'e �ikarildi, y�kseklikleri optimize edildi.

### [28.04.2026 08:02] - Ofis Cihazi Ekleme Butonu Konum ve Stil G�ncellemesi
- **Talimat**: Ofis cihazi ekleme butonunu liste basligina tasi, yesil yap ve hover efekti ekle.
- **Islem**: Sag alttaki y�zer mavi '+' butonu kaldirildi.
- **Islem**: Ofis cihazlari listesi (Cihaz Listesi) basligina yeni bir yesil '+' butonu eklendi.
- **Islem**: Buton rengi yesil (#10B981), �zerine gelindiginde ise koyu yesil (#059669) olacak sekilde g�ncellendi.
- **Islem**: Liste basligi yazisi (Cihaz Listesi) g�rselle uyumlu olmasi i�in kirmizi (#EF4444) renge boyandi.

### [28.04.2026 08:04] - Ofis Cihazi Ekleme Butonu Iyilestirmesi
- **Hata Giderimi**: Butonun baslik yazisi �zerine binmesi sorunu 'DockPanel' ve header style d�zenlemeleri ile giderildi.
- **Hata Giderimi**: Butonun �alismama (binding) sorunu, 'x:Reference root' ve 'CanUserSort=False' kullanilarak ��z�ld�.

### [28.04.2026 08:07] - D�ng�sel Bagimlilik (Cyclical Dependency) ve �alismama Sorunu Giderildi
- **Hata Giderimi**: Uygulamanin a�ilista ��kmesine neden olan 'cyclical dependency' hatasi, butonun DataGrid basligindan disariya (�st satira) tasinmasiyla giderildi.
- **Hata Giderimi**: Butonun �alismama sorunu, DataGrid sablon kapsamindan �ikarilip dogrudan ViewModel baglamina alinarak ��z�ld�.
- **Tasarim**: Liste basligi ('Cihaz Listesi' yazisi ve yesil '+' butonu) artik DataGrid'in hemen �zerinde, ayri bir satirda daha stabil bir sekilde durmaktadir.
