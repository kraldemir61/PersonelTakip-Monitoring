using System.Text.Json;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Dapper;
using Npgsql;
using PersonelTakip.Models;

namespace PersonelTakip.Services;

public class DatabaseService
{
    static DatabaseService()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public DatabaseService()
    {
    }

    private NpgsqlConnection CreateConnection() => new(AppConfiguration.Instance.Database.ConnectionString);

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            using var conn = CreateConnection();
            // Bağlantı kontrolü için kısa bir timeout (2 saniye)
            var csb = new NpgsqlConnectionStringBuilder(conn.ConnectionString) { Timeout = 2 };
            conn.ConnectionString = csb.ToString();
            
            await conn.OpenAsync();
            await conn.ExecuteAsync("SELECT 1");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DB Connection Error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> InitializeDatabaseAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            
            // Tek bir dev işlemde tüm şemayı ve onarımları yapıyoruz
            await conn.ExecuteAsync(@"
                -- 1. TEMEL LOOKUP TABLOLARI
                CREATE TABLE IF NOT EXISTS bolumler (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS gorevler (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS uyruklar (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS para_birimleri (id SERIAL PRIMARY KEY, adi VARCHAR(50) NOT NULL);
                CREATE TABLE IF NOT EXISTS cihaz_adlari (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS cihaz_markalari (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS cihaz_modelleri (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS cihaz_firmalari (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);

                -- 2. ANA TABLOLAR
                CREATE TABLE IF NOT EXISTS santiyeler (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    adi TEXT NOT NULL,
                    kod TEXT UNIQUE NOT NULL,
                    adres TEXT,
                    telefon TEXT,
                    aktif BOOLEAN DEFAULT true,
                    created_at TIMESTAMPTZ DEFAULT now(),
                    updated_at TIMESTAMPTZ DEFAULT now()
                );

                CREATE TABLE IF NOT EXISTS kullanicilar (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    kullanici_adi TEXT UNIQUE NOT NULL,
                    email TEXT UNIQUE NOT NULL,
                    sifre_hash TEXT NOT NULL,
                    rol TEXT NOT NULL DEFAULT 'User',
                    santiye_id UUID REFERENCES santiyeler(id),
                    aktif BOOLEAN DEFAULT true,
                    son_giris TIMESTAMPTZ,
                    created_at TIMESTAMPTZ DEFAULT now()
                );

                CREATE TABLE IF NOT EXISTS personeller (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    santiye_id UUID REFERENCES santiyeler(id),
                    adi_soyadi TEXT NOT NULL,
                    bolumu INTEGER REFERENCES bolumler(id),
                    gorevi INTEGER REFERENCES gorevler(id),
                    uyrugu INTEGER REFERENCES uyruklar(id),
                    para_birimi INTEGER REFERENCES para_birimleri(id),
                    ise_giris_tarihi DATE,
                    telefon_numarasi TEXT,
                    maas NUMERIC(15, 2),
                    aktif BOOLEAN DEFAULT true,
                    created_at TIMESTAMPTZ DEFAULT now()
                );

                CREATE TABLE IF NOT EXISTS cihazlar (
                    id UUID PRIMARY KEY,
                    seri_no TEXT,
                    cihaz_adi TEXT,
                    marka TEXT,
                    model TEXT,
                    ozellik TEXT,
                    sahip_firma TEXT,
                    not_text TEXT,
                    tur INTEGER,
                    santiye_id UUID REFERENCES santiyeler(id),
                    durum TEXT,
                    son_islem_tarihi TIMESTAMP,
                    foto_path TEXT,
                    zimmetli_personel_id UUID REFERENCES personeller(id),
                    zimmet_tarihi TIMESTAMP
                );
                
                -- CİHAZLAR TABLOSU GÜNCELLEMELERİ (ALTER TABLE if not exists)
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='cihazlar' AND column_name='foto_path') THEN
                        ALTER TABLE cihazlar ADD COLUMN foto_path TEXT;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='cihazlar' AND column_name='zimmetli_personel_id') THEN
                        ALTER TABLE cihazlar ADD COLUMN zimmetli_personel_id UUID REFERENCES personeller(id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='cihazlar' AND column_name='zimmet_tarihi') THEN
                        ALTER TABLE cihazlar ADD COLUMN zimmet_tarihi TIMESTAMP;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='cihazlar' AND column_name='ozellik') THEN
                        ALTER TABLE cihazlar ADD COLUMN ozellik TEXT;
                    END IF;
                    
                    -- CİHAZ_ZİMMET_GECMİSİ TABLOSU GÜNCELLEMELERİ
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='cihaz_zimmet_gecmisi' AND column_name='iade_aciklamasi') THEN
                        ALTER TABLE cihaz_zimmet_gecmisi ADD COLUMN iade_aciklamasi TEXT;
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS cihaz_zimmet_gecmisi (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    cihaz_id UUID REFERENCES cihazlar(id) ON DELETE CASCADE,
                    personel_id UUID REFERENCES personeller(id) ON DELETE CASCADE,
                    zimmet_tarihi TIMESTAMP NOT NULL,
                    iade_tarihi TIMESTAMP,
                    aciklama TEXT
                );

                CREATE TABLE IF NOT EXISTS cihaz_lisanslari (
                    hardware_id TEXT PRIMARY KEY,
                    license_key TEXT,
                    demo_data TEXT,
                    updated_at TIMESTAMPTZ DEFAULT now()
                );

                -- 3. HAREKET VE LOG TABLOLARI
                CREATE TABLE IF NOT EXISTS hareketler (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    h_personel_id UUID REFERENCES personeller(id) ON DELETE CASCADE,
                    tarih TIMESTAMPTZ DEFAULT now(),
                    aciklama TEXT
                );

                CREATE TABLE IF NOT EXISTS cihaz_hareketleri (
                    id UUID PRIMARY KEY,
                    cihaz_id UUID REFERENCES cihazlar(id),
                    nereden_santiye_id UUID REFERENCES santiyeler(id),
                    nereye_santiye_id UUID REFERENCES santiyeler(id),
                    personel_id UUID REFERENCES personeller(id),
                    tarih TIMESTAMP NOT NULL,
                    kullanici_id UUID REFERENCES kullanicilar(id),
                    aciklama TEXT,
                    islem_turu TEXT
                );

                -- Eğer personel_id yoksa ekle (Upgrade senaryosu)
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='cihaz_hareketleri' AND column_name='personel_id') THEN
                        ALTER TABLE cihaz_hareketleri ADD COLUMN personel_id UUID REFERENCES personeller(id);
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS bildirimler (
                    id SERIAL PRIMARY KEY,
                    mesaj TEXT NOT NULL,
                    tetikleyen_kullanici_id UUID NOT NULL,
                    tarih TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS bildirim_durumlari (
                    kullanici_id UUID NOT NULL,
                    bildirim_id INT NOT NULL,
                    okundu_mu BOOLEAN NOT NULL DEFAULT FALSE,
                    silindi_mi BOOLEAN NOT NULL DEFAULT FALSE,
                    PRIMARY KEY (kullanici_id, bildirim_id)
                );

                CREATE TABLE IF NOT EXISTS audit_log (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    kullanici_id UUID REFERENCES kullanicilar(id),
                    kullanici_adi TEXT,
                    tablo_adi TEXT NOT NULL,
                    kayit_id TEXT,
                    islem_tipi TEXT NOT NULL,
                    eski_deger JSONB,
                    yeni_deger JSONB,
                    ip_adresi TEXT,
                    aciklama TEXT,
                    tarih TIMESTAMPTZ DEFAULT now()
                );

                -- 4. AGRESİF SÜTUN ONARIMI (Eğer tablo varsa ama sütun eksikse ekler)
                ALTER TABLE santiyeler ADD COLUMN IF NOT EXISTS adres TEXT;
                ALTER TABLE santiyeler ADD COLUMN IF NOT EXISTS telefon TEXT;
                ALTER TABLE santiyeler ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ DEFAULT now();
                ALTER TABLE santiyeler ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT now();

                ALTER TABLE kullanicilar ADD COLUMN IF NOT EXISTS son_giris TIMESTAMPTZ;
                ALTER TABLE kullanicilar ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ DEFAULT now();

                ALTER TABLE personeller ADD COLUMN IF NOT EXISTS bolumu INTEGER;
                ALTER TABLE personeller ADD COLUMN IF NOT EXISTS gorevi INTEGER;
                ALTER TABLE personeller ADD COLUMN IF NOT EXISTS uyrugu INTEGER;
                ALTER TABLE personeller ADD COLUMN IF NOT EXISTS para_birimi INTEGER;
                ALTER TABLE personeller ADD COLUMN IF NOT EXISTS ise_giris_tarihi DATE;
                ALTER TABLE personeller ADD COLUMN IF NOT EXISTS telefon_numarasi TEXT;
                ALTER TABLE personeller ADD COLUMN IF NOT EXISTS maas NUMERIC(15, 2);
                ALTER TABLE personeller ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ DEFAULT now();

                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS seri_no TEXT;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS cihaz_adi TEXT;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS marka TEXT;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS model TEXT;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS sahip_firma TEXT;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS not_text TEXT;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS tur INTEGER;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS durum TEXT;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS son_islem_tarihi TIMESTAMP;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS foto_path TEXT;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS zimmetli_personel_id UUID;
                ALTER TABLE cihazlar ADD COLUMN IF NOT EXISTS zimmet_tarihi TIMESTAMP;

                ALTER TABLE cihaz_hareketleri ADD COLUMN IF NOT EXISTS cihaz_id UUID;
                ALTER TABLE cihaz_hareketleri ADD COLUMN IF NOT EXISTS nereden_santiye_id UUID;
                ALTER TABLE cihaz_hareketleri ADD COLUMN IF NOT EXISTS nereye_santiye_id UUID;
                ALTER TABLE cihaz_hareketleri ADD COLUMN IF NOT EXISTS tarih TIMESTAMP;
                ALTER TABLE cihaz_hareketleri ADD COLUMN IF NOT EXISTS kullanici_id UUID;
                ALTER TABLE cihaz_hareketleri ADD COLUMN IF NOT EXISTS aciklama TEXT;
                ALTER TABLE cihaz_hareketleri ADD COLUMN IF NOT EXISTS islem_turu TEXT;

                ALTER TABLE hareketler ADD COLUMN IF NOT EXISTS aciklama TEXT;
                
                ALTER TABLE bildirimler ADD COLUMN IF NOT EXISTS mesaj TEXT;
                ALTER TABLE bildirimler ADD COLUMN IF NOT EXISTS tetikleyen_kullanici_id UUID;
                ALTER TABLE bildirimler ADD COLUMN IF NOT EXISTS tarih TIMESTAMP DEFAULT now();
                ALTER TABLE bildirimler ADD COLUMN IF NOT EXISTS okundu_mu BOOLEAN DEFAULT false;
                ALTER TABLE bildirimler ADD COLUMN IF NOT EXISTS silindi_mi BOOLEAN DEFAULT false;

                ALTER TABLE bildirim_durumlari ADD COLUMN IF NOT EXISTS okundu_mu BOOLEAN DEFAULT false;
                ALTER TABLE bildirim_durumlari ADD COLUMN IF NOT EXISTS silindi_mi BOOLEAN DEFAULT false;

                ALTER TABLE audit_log ADD COLUMN IF NOT EXISTS kullanici_adi TEXT;
                ALTER TABLE audit_log ADD COLUMN IF NOT EXISTS ip_adresi TEXT;
                ALTER TABLE audit_log ADD COLUMN IF NOT EXISTS aciklama TEXT;

                -- Tip Onarımları
                DO $$ BEGIN 
                    IF (SELECT data_type FROM information_schema.columns WHERE table_name='personeller' AND column_name='para_birimi') = 'text' THEN 
                        ALTER TABLE personeller ALTER COLUMN para_birimi TYPE INTEGER USING para_birimi::integer; 
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='hareketler' AND column_name='personel_id') THEN 
                        ALTER TABLE hareketler RENAME COLUMN personel_id TO h_personel_id; 
                    END IF;
                END $$;
            ");

            // 5. VARSAYILAN ADMİN KONTROLÜ
            var adminCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM kullanicilar");
            if (adminCount == 0)
            {
                var adminHash = BCrypt.Net.BCrypt.HashPassword("Admin123");
                await conn.ExecuteAsync(@"
                    INSERT INTO kullanicilar (id, kullanici_adi, email, sifre_hash, rol, aktif) 
                    VALUES (@Id, 'Admin', 'admin@system.local', @Hash, 'Admin', true)",
                    new { Id = Guid.NewGuid(), Hash = adminHash });
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DB Initialization Error: {ex.Message}");
            throw;
        }
    }

    public async Task<Kullanici?> KullaniciGirisAsync(string kullaniciAdi, string sifre)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var kullanici = await conn.QueryFirstOrDefaultAsync<Kullanici>(
            @"SELECT k.id, k.kullanici_adi, k.email, k.sifre_hash AS SifreHash, k.rol, k.santiye_id, k.aktif, k.son_giris, k.created_at, s.adi AS SantiyeAdi, s.kod AS SantiyeKod
              FROM kullanicilar k 
              LEFT JOIN santiyeler s ON k.santiye_id = s.id 
              WHERE k.kullanici_adi = @KullaniciAdi AND k.aktif = true",
            new { KullaniciAdi = kullaniciAdi });

        if (kullanici == null) return null;

        try
        {
            if (!string.IsNullOrEmpty(kullanici.SifreHash) && BCrypt.Net.BCrypt.Verify(sifre, kullanici.SifreHash))
            {
                await conn.ExecuteAsync(
                    "UPDATE kullanicilar SET son_giris = @Now WHERE id = @Id",
                    new { kullanici.Id, Now = DateTime.UtcNow });
                return kullanici;
            }
        }
        catch { }

        return null;
    }

    public async Task NotifySystemAsync(Guid tetikleyenKullaniciId, string mesaj)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        // 1. Veritabanına kaydet
        await conn.ExecuteAsync(
            "INSERT INTO bildirimler (mesaj, tetikleyen_kullanici_id, tarih) VALUES (@Mesaj, @TetikleyenId, NOW())",
            new { Mesaj = mesaj, TetikleyenId = tetikleyenKullaniciId });

        // 2. LISTEN/NOTIFY ile kanala gönder
        await conn.ExecuteAsync($"NOTIFY personel_bildirim, '{tetikleyenKullaniciId}|{mesaj}'");
    }

    public async Task<Kullanici?> KullaniciGetirIdAsync(Guid id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QueryFirstOrDefaultAsync<Kullanici>(
            @"SELECT k.*, s.adi AS SantiyeAdi
              FROM kullanicilar k
              LEFT JOIN santiyeler s ON k.santiye_id = s.id
              WHERE k.id = @Id", new { Id = id });
    }

    public async Task<int?> CheckAndAddLookupAsync(string table, List<LookupItem> list, string value)
    {
        var normalizedValue = Helpers.StringHelper.NormalizeTurkish(value);
        var existing = list.FirstOrDefault(l => Helpers.StringHelper.NormalizeTurkish(l.Adi) == normalizedValue);
        if (existing != null) return existing.Id;

        // Veritabanına ekle
        var newId = await LookupOlusturAsync(table, value);
        // Listeye de ekle ki aynı excelde tekrar edenler için tekrar veritabanına gitmesin
        list.Add(new LookupItem { Id = newId, Adi = value });
        return newId;
    }

    #region Ofis Cihazı İşlemleri (Ayrı Kodlar)

    public async Task OfisCihaziEkleAsync(OfisCihazi cihaz)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO cihazlar (id, seri_no, cihaz_adi, marka, model, ozellik, sahip_firma, not_text, tur, durum, son_islem_tarihi, foto_path, santiye_id)
            VALUES (@Id, @SeriNo, @CihazAdi, @Marka, @Model, @Ozellik, NULL, @Not, 1, 
                    CASE WHEN @SantiyeId IS NOT NULL THEN 'Şantiyede' ELSE 'Boşta' END, 
                    @SonIslemTarihi, @FotoPath, @SantiyeId)",
            cihaz);
    }

    public async Task OfisCihaziGuncelleAsync(OfisCihazi cihaz)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            UPDATE cihazlar SET 
                seri_no = @SeriNo, 
                cihaz_adi = @CihazAdi, 
                marka = @Marka, 
                model = @Model, 
                ozellik = @Ozellik,
                not_text = @Not, 
                foto_path = @FotoPath,
                santiye_id = @SantiyeId,
                durum = CASE WHEN zimmetli_personel_id IS NOT NULL THEN 'Zimmetli' 
                             WHEN @SantiyeId IS NOT NULL THEN 'Şantiyede' 
                             ELSE 'Boşta' END,
                son_islem_tarihi = @SonIslemTarihi
            WHERE id = @Id",
            cihaz);
    }

    public async Task OfisCihaziZimmetleAsync(Guid cihazId, Guid personelId, DateTime tarih, string aciklama)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(@"
                UPDATE cihazlar SET 
                    zimmetli_personel_id = @PersonelId, 
                    zimmet_tarihi = @Tarih, 
                    durum = 'Zimmetli',
                    son_islem_tarihi = @Tarih
                WHERE id = @CihazId",
                new { CihazId = cihazId, PersonelId = personelId, Tarih = tarih }, trans);

            await conn.ExecuteAsync(@"
                INSERT INTO cihaz_zimmet_gecmisi (cihaz_id, personel_id, zimmet_tarihi, aciklama)
                VALUES (@CihazId, @PersonelId, @Tarih, @Aciklama)",
                new { CihazId = cihazId, PersonelId = personelId, Tarih = tarih, Aciklama = aciklama }, trans);

            await trans.CommitAsync();
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }

    public async Task OfisCihaziIadeAlAsync(Guid cihazId, DateTime tarih, string aciklama)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(@"
                UPDATE cihazlar SET 
                    zimmetli_personel_id = NULL, 
                    zimmet_tarihi = NULL, 
                    durum = 'Boşta',
                    son_islem_tarihi = @Tarih
                WHERE id = @CihazId",
                new { CihazId = cihazId, Tarih = tarih }, trans);

            await conn.ExecuteAsync(@"
                UPDATE cihaz_zimmet_gecmisi SET 
                    iade_tarihi = @Tarih, 
                    iade_aciklamasi = @Aciklama
                WHERE cihaz_id = @CihazId AND iade_tarihi IS NULL",
                new { CihazId = cihazId, Tarih = tarih, Aciklama = aciklama }, trans);

            await trans.CommitAsync();
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }

    public async Task<List<OfisCihazi>> OfisCihazlariniGetirAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<OfisCihazi>(@"
            SELECT c.id, c.seri_no, c.cihaz_adi, c.marka, c.model, c.ozellik, c.not_text as Not, 
                   c.durum, c.son_islem_tarihi, c.foto_path,
                   c.zimmetli_personel_id, p.adi_soyadi as ZimmetliPersonelAd, c.zimmet_tarihi,
                   s.kod as BulunduguSantiyeKod, s.id as SantiyeId
            FROM cihazlar c
            LEFT JOIN personeller p ON c.zimmetli_personel_id = p.id
            LEFT JOIN santiyeler s ON c.santiye_id = s.id
            WHERE c.tur = 1
            ORDER BY c.son_islem_tarihi DESC")).ToList();
    }

    #endregion

    public async Task ExecuteSqlAsync(string sql)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql);
    }

    public async Task<List<Kullanici>> KullanicilariGetirAsync(Guid? santiyeId = null, string? rol = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = @"SELECT k.*, s.adi AS SantiyeAdi, s.kod AS SantiyeKod
                    FROM kullanicilar k
                    LEFT JOIN santiyeler s ON k.santiye_id = s.id
                    WHERE k.aktif = true";

        if (santiyeId.HasValue) sql += " AND k.santiye_id = @SantiyeId";
        if (!string.IsNullOrEmpty(rol)) sql += " AND k.rol = @Rol";

        sql += " ORDER BY k.kullanici_adi";

        var result = await conn.QueryAsync<Kullanici>(sql, new { SantiyeId = santiyeId, Rol = rol });
        return result.ToList();
    }

    public async Task<int> GetSantiyeKullaniciSayisiAsync(Guid santiyeId, Guid? excludeUserId = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        var sql = "SELECT COUNT(*) FROM kullanicilar WHERE santiye_id = @SantiyeId AND aktif = true";
        if (excludeUserId.HasValue) sql += " AND id != @ExcludeId";
        return await conn.ExecuteScalarAsync<int>(sql, new { SantiyeId = santiyeId, ExcludeId = excludeUserId });
    }

    public async Task<Guid> KullaniciOlusturAsync(Kullanici kullanici, string sifre)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        // 1. Mükerrer kullanıcı adı kontrolü (Büyük-küçük harf duyarsız)
        var varMi = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM kullanicilar WHERE LOWER(kullanici_adi) = LOWER(@KullaniciAdi))",
            new { KullaniciAdi = kullanici.KullaniciAdi });

        if (varMi)
        {
            throw new InvalidOperationException($"'{kullanici.KullaniciAdi}' kullanıcı adı zaten alınmış. Lütfen başka bir kullanıcı adı seçin.");
        }

        if (kullanici.SantiyeId.HasValue)
        {
            var sayi = await GetSantiyeKullaniciSayisiAsync(kullanici.SantiyeId.Value);
            if (sayi >= 1)
            {
                throw new InvalidOperationException("Bu şantiyeye atanmış bir kullanıcı zaten mevcut. Her şantiye için sadece tek bir kullanıcı tanımlanabilir.");
            }
        }

        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            var id = Guid.NewGuid();
            var sifreHash = BCrypt.Net.BCrypt.HashPassword(sifre);

            await conn.ExecuteAsync(
                @"INSERT INTO kullanicilar (id, kullanici_adi, email, sifre_hash, rol, santiye_id, aktif, created_at)
                  VALUES (@Id, @KullaniciAdi, @Email, @SifreHash, @Rol, @SantiyeId, true, @Now)",
                new { Id = id, kullanici.KullaniciAdi, Email = string.IsNullOrWhiteSpace(kullanici.Email) ? $"{kullanici.KullaniciAdi}@system.local" : kullanici.Email, SifreHash = sifreHash, kullanici.Rol, kullanici.SantiyeId, Now = DateTime.UtcNow },
                transaction);

            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task KullaniciGuncelleAsync(Kullanici kullanici, Guid adminId)
    {
        if (kullanici.SantiyeId.HasValue)
        {
            var sayi = await GetSantiyeKullaniciSayisiAsync(kullanici.SantiyeId.Value, kullanici.Id);
            if (sayi >= 1)
            {
                throw new InvalidOperationException("Bu şantiyeye atanmış bir kullanıcı zaten mevcut. Kullanıcı bu şantiyeye atanamaz.");
            }
        }

        using var conn = CreateConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync(
            @"UPDATE kullanicilar SET kullanici_adi = @KullaniciAdi,
              rol = @Rol, santiye_id = @SantiyeId WHERE id = @Id",
            new { kullanici.Id, kullanici.KullaniciAdi, kullanici.Rol, kullanici.SantiyeId });

        await AuditLogAsync(adminId, "kullanicilar", kullanici.Id.ToString(), "Guncelle",
            null, JsonSerializer.Serialize(new { kullanici.Rol, kullanici.SantiyeId }), "Kullanıcı güncellendi");
    }

    public async Task KullaniciSilAsync(Guid id, Guid adminId)
    {
        Exception? lastEx = null;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                var onceki = await conn.QueryFirstOrDefaultAsync<Kullanici>(
                    "SELECT * FROM kullanicilar WHERE id = @Id AND aktif = true",
                    new { Id = id });

                if (onceki == null)
                {
                    throw new InvalidOperationException("Kullanıcı bulunamadı veya zaten silinmiş.");
                }

                await conn.ExecuteAsync(
                    "UPDATE kullanicilar SET aktif = false WHERE id = @Id",
                    new { Id = id });

                var guncel = await conn.QueryFirstOrDefaultAsync<Kullanici>(
                    "SELECT * FROM kullanicilar WHERE id = @Id",
                    new { Id = id });

                if (guncel == null || guncel.Aktif == false)
                {
                    await AuditLogAsync(adminId, "kullanicilar", id.ToString(), "Sil",
                        JsonSerializer.Serialize(new { Aktif = true }),
                        JsonSerializer.Serialize(new { Aktif = false }),
                        "Kullanıcı pasifize edildi");
                }
                return;
            }
            catch (Exception ex) when (i < 2 && !(ex is InvalidOperationException))
            {
                lastEx = ex;
                await Task.Delay(500);
            }
            catch
            {
                throw;
            }
        }
        throw lastEx ?? new Exception("Silme işlemi başarısız oldu.");
    }

    public async Task KullaniciRolGuncelleAsync(Guid adminId, Guid hedefId, string yeniRol)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync(
            "UPDATE kullanicilar SET rol = @YeniRol WHERE id = @Id",
            new { Id = hedefId, YeniRol = yeniRol });

        await AuditLogAsync(adminId, "kullanicilar", hedefId.ToString(), "RolDegistir",
            null, JsonSerializer.Serialize(new { Rol = yeniRol }), "Rol değiştirildi");
    }

    public async Task AdminParolaSifirlaAsync(Guid hedefKullaniciId, string yeniSifre, Guid adminId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sifreHash = BCrypt.Net.BCrypt.HashPassword(yeniSifre);
        await conn.ExecuteAsync(
            "UPDATE kullanicilar SET sifre_hash = @Hash WHERE id = @Id",
            new { Id = hedefKullaniciId, Hash = sifreHash });

        await AuditLogAsync(adminId, "kullanicilar", hedefKullaniciId.ToString(), "Guncelle",
            null, null, $"Kullanıcı şifresi admin tarafından '{yeniSifre}' olarak sıfırlandı.");
    }

    public async Task ParolaDegistirAsync(Guid kullaniciId, string eskiSifre, string yeniSifre)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var kullanici = await conn.QueryFirstOrDefaultAsync<Kullanici>(
            "SELECT sifre_hash AS SifreHash FROM kullanicilar WHERE id = @Id",
            new { Id = kullaniciId });

        if (kullanici == null || string.IsNullOrEmpty(kullanici.SifreHash) || !BCrypt.Net.BCrypt.Verify(eskiSifre, kullanici.SifreHash))
        {
            throw new Exception("Mevcut şifreniz hatalı.");
        }

        var yeniHash = BCrypt.Net.BCrypt.HashPassword(yeniSifre);
        await conn.ExecuteAsync(
            "UPDATE kullanicilar SET sifre_hash = @Hash WHERE id = @Id",
            new { Id = kullaniciId, Hash = yeniHash });

        await AuditLogAsync(kullaniciId, "kullanicilar", kullaniciId.ToString(), "Guncelle",
            null, null, "Kullanıcı kendi şifresini değiştirdi.");
    }

    public async Task<List<Santiye>> SantiyeleriGetirAsync(bool? aktif = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = "SELECT * FROM santiyeler WHERE 1=1";
        if (aktif.HasValue) sql += " AND aktif = @Aktif";
        sql += " ORDER BY adi";

        var result = await conn.QueryAsync<Santiye>(sql, new { Aktif = aktif });
        return result.ToList();
    }

    public async Task<Guid> SantiyeOlusturAsync(Santiye santiye)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var id = Guid.NewGuid();
        await conn.ExecuteAsync(
            @"INSERT INTO santiyeler (id, adi, kod, adres, aktif, created_at, updated_at)
              VALUES (@Id, @Adi, @Kod, @Adres, true, @Now, @Now)",
            new { Id = id, santiye.Adi, santiye.Kod, santiye.Adres, Now = DateTime.UtcNow });

        return id;
    }

    public async Task SantiyeGuncelleAsync(Santiye santiye, Guid adminId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync(
            @"UPDATE santiyeler SET adi = @Adi, kod = @Kod, adres = @Adres, updated_at = @Now
              WHERE id = @Id",
            new { santiye.Id, santiye.Adi, santiye.Kod, santiye.Adres, Now = DateTime.UtcNow });

        await AuditLogAsync(adminId, "santiyeler", santiye.Id.ToString(), "Guncelle",
            null, JsonSerializer.Serialize(new { santiye.Adi, santiye.Kod }), "Şantiye güncellendi");
    }

    public async Task SantiyeSilAsync(Guid id, Guid adminId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var onceki = await conn.QueryFirstOrDefaultAsync<Santiye>(
            "SELECT * FROM santiyeler WHERE id = @Id",
            new { Id = id });

        if (onceki == null) throw new InvalidOperationException("Şantiye bulunamadı.");

        await conn.ExecuteAsync(
            "UPDATE santiyeler SET aktif = false WHERE id = @Id",
            new { Id = id });

        await AuditLogAsync(adminId, "santiyeler", id.ToString(), "Sil",
            JsonSerializer.Serialize(new { Aktif = true }),
            JsonSerializer.Serialize(new { Aktif = false }),
            "Şantiye pasifize edildi");
    }

    public async Task SantiyeAktifEtAsync(Guid id, Guid adminId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var onceki = await conn.QueryFirstOrDefaultAsync<Santiye>(
            "SELECT * FROM santiyeler WHERE id = @Id",
            new { Id = id });

        if (onceki == null) throw new InvalidOperationException("Şantiye bulunamadı.");

        await conn.ExecuteAsync(
            "UPDATE santiyeler SET aktif = true WHERE id = @Id",
            new { Id = id });

        await AuditLogAsync(adminId, "santiyeler", id.ToString(), "Guncelle",
            JsonSerializer.Serialize(new { Aktif = false }),
            JsonSerializer.Serialize(new { Aktif = true }),
            "Şantiye aktifleştirildi");
    }

    public async Task<List<LookupItem>> BolumleriGetirAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        var result = await conn.QueryAsync<LookupItem>("SELECT id, adi FROM bolumler ORDER BY adi");
        return result.ToList();
    }

    public async Task<List<LookupItem>> GorevleriGetirAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        var result = await conn.QueryAsync<LookupItem>("SELECT id, adi FROM gorevler ORDER BY adi");
        return result.ToList();
    }

    public async Task<List<LookupItem>> UyruklariGetirAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        var result = await conn.QueryAsync<LookupItem>("SELECT id, adi FROM uyruklar ORDER BY adi");
        return result.ToList();
    }

    public async Task<List<LookupItem>> ParaBirimleriniGetirAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        
        // Tablo yoksa oluştur
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS para_birimleri (
                id SERIAL PRIMARY KEY,
                adi VARCHAR(50) NOT NULL
            )");

        // Personeller tablosundaki para_birimi sütununu integer'a çevirmeyi dene (eskiden string idi)
        try 
        {
            await conn.ExecuteAsync(@"
                ALTER TABLE personeller 
                ALTER COLUMN para_birimi TYPE INTEGER 
                USING (CASE WHEN para_birimi ~ '^[0-9]+$' THEN para_birimi::integer ELSE NULL END)");
        } catch { /* Sütun zaten integer olabilir veya işlem kısıtlıdır */ }

        var result = await conn.QueryAsync<LookupItem>("SELECT id, adi FROM para_birimleri ORDER BY adi");
        return result.ToList();
    }

    public async Task<List<LookupItem>> LookupGetirAsync(string tablo)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        var result = await conn.QueryAsync<LookupItem>($"SELECT id, adi FROM {tablo} ORDER BY adi");
        return result.ToList();
    }

    public async Task<int> LookupOlusturAsync(string tablo, string adi)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var id = await conn.QueryFirstAsync<int>(
            $@"INSERT INTO {tablo} (adi) VALUES (@Adi) RETURNING id;",
            new { Adi = adi });

        return id;
    }

    public async Task LookupGuncelleAsync(string tablo, int id, string adi)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync(
            $"UPDATE {tablo} SET adi = @Adi WHERE id = @Id",
            new { Id = id, Adi = adi });
    }

    public async Task LookupSilAsync(string tablo, int id)
    {
        Exception? lastEx = null;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                string kontrolSql;
                if (tablo.StartsWith("cihaz_"))
                {
                    var kolon = tablo switch
                    {
                        "cihaz_adlari" => "cihaz_adi",
                        "cihaz_markalari" => "marka",
                        "cihaz_modelleri" => "model",
                        "cihaz_firmalari" => "sahip_firma",
                        _ => "cihaz_adi"
                    };
                    
                    // Cihazlarda lookup ID yerine direkt isim tutuluyor olabilir (mevcut yapıya göre)
                    // Eğer isim tutuluyorsa önce ismini alıp sonra cihazlar tablosunda o ismi aramalıyız.
                    var isim = await conn.ExecuteScalarAsync<string>($"SELECT adi FROM {tablo} WHERE id = @Id", new { Id = id });
                    kontrolSql = $"SELECT COUNT(*) FROM cihazlar WHERE {kolon} = @Isim";
                    var sayiCihaz = await conn.ExecuteScalarAsync<int>(kontrolSql, new { Isim = isim });
                    if (sayiCihaz > 0)
                        throw new InvalidOperationException($"Bu kayıt {sayiCihaz} cihazda kullanıldığı için silinemez.");
                }
                else
                {
                    var tabloAdi = tablo.ToLower() switch
                    {
                        "bolumler" => "bolumu",
                        "gorevler" => "gorevi",
                        "uyruklar" => "uyrugu",
                        "para_birimleri" => "para_birimi",
                        _ => tablo.ToLower().TrimEnd('r')
                    };

                    kontrolSql = $"SELECT COUNT(*) FROM personeller WHERE {tabloAdi} = @Id";
                    var sayi = await conn.ExecuteScalarAsync<int>(kontrolSql, new { Id = id });

                    if (sayi > 0)
                    {
                        throw new InvalidOperationException($"Bu kayıt {sayi} personelinin başvurusunda kullanıldığı için silinemez.");
                    }
                }

                await conn.ExecuteAsync(
                    $"DELETE FROM {tablo} WHERE id = @Id",
                    new { Id = id });
                return;
            }
            catch (Exception ex) when (i < 2 && !(ex is InvalidOperationException))
            {
                lastEx = ex;
                await Task.Delay(500);
            }
            catch
            {
                throw;
            }
        }
        throw lastEx ?? new Exception("Silme işlemi başarısız oldu.");
    }

    public async Task<List<Personel>> PersonelleriGetirAsync(Guid? santiyeId = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = @"SELECT p.*,
                    s.adi AS SantiyeAdiDisplay,
                    s.kod AS SantiyeKod,
                    b.adi AS BolumuDisplay,
                    g.adi AS GoreviDisplay,
                    u.adi AS UyruguDisplay,
                    pb.adi AS ParaBirimiDisplay
                    FROM personeller p
                    LEFT JOIN santiyeler s ON p.santiye_id = s.id
                    LEFT JOIN bolumler b ON p.bolumu = b.id
                    LEFT JOIN gorevler g ON p.gorevi = g.id
                    LEFT JOIN uyruklar u ON p.uyrugu = u.id
                    LEFT JOIN para_birimleri pb ON p.para_birimi = pb.id
                    WHERE p.aktif = true";

        if (santiyeId.HasValue) sql += " AND p.santiye_id = @SantiyeId";
        sql += " ORDER BY p.adi_soyadi";

        var result = await conn.QueryAsync<Personel>(sql, new { SantiyeId = santiyeId });
        return result.ToList();
    }

    public async Task<Personel?> PersonelGetirIdAsync(Guid id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = @"SELECT p.*,
                    s.adi AS SantiyeAdiDisplay,
                    s.kod AS SantiyeKod,
                    b.adi AS BolumuDisplay,
                    g.adi AS GoreviDisplay,
                    u.adi AS UyruguDisplay,
                    pb.adi AS ParaBirimiDisplay
                    FROM personeller p
                    LEFT JOIN santiyeler s ON p.santiye_id = s.id
                    LEFT JOIN bolumler b ON p.bolumu = b.id
                    LEFT JOIN gorevler g ON p.gorevi = g.id
                    LEFT JOIN uyruklar u ON p.uyrugu = u.id
                    LEFT JOIN para_birimleri pb ON p.para_birimi = pb.id
                    WHERE p.id = @Id";

        return await conn.QueryFirstOrDefaultAsync<Personel>(sql, new { Id = id });
    }

    public async Task<Guid> PersonelOlusturAsync(Personel personel)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var id = Guid.NewGuid();
        await conn.ExecuteAsync(
            @"INSERT INTO personeller (id, santiye_id, adi_soyadi, bolumu, gorevi, uyrugu, para_birimi, ise_giris_tarihi, telefon_numarasi, maas, aktif, created_at)
              VALUES (@Id, @SantiyeId, @AdiSoyadi, @Bolumu, @Gorevi, @Uyrugu, @ParaBirimi, @IseGirisTarihi, @TelefonNumarasi, @Maas, true, @Now)",
            new { Id = id, personel.SantiyeId, personel.AdiSoyadi, personel.Bolumu, personel.Gorevi, personel.Uyrugu, personel.ParaBirimi, personel.IseGirisTarihi, personel.TelefonNumarasi, personel.Maas, Now = DateTime.UtcNow });

        return id;
    }

    public async Task PersonelGuncelleAsync(Personel personel)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync(
            @"UPDATE personeller SET
                adi_soyadi = @AdiSoyadi, santiye_id = @SantiyeId, bolumu = @Bolumu,
                gorevi = @Gorevi, uyrugu = @Uyrugu, para_birimi = @ParaBirimi, ise_giris_tarihi = @IseGirisTarihi,
                telefon_numarasi = @TelefonNumarasi, maas = @Maas
              WHERE id = @Id",
            personel);
    }

    public async Task PersonelSilAsync(Guid id, Guid kullaniciId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            await conn.ExecuteAsync(
                "UPDATE personeller SET aktif = false WHERE id = @Id",
                new { Id = id }, transaction);

            await conn.ExecuteAsync(
                @"INSERT INTO hareketler (id, h_personel_id, tarih, aciklama)
                  VALUES (@Id, @HPersonelId, @Tarih, @Aciklama)",
                new { Id = Guid.NewGuid(), HPersonelId = id, Tarih = DateTime.UtcNow, Aciklama = "İşten Ayrılma" },
                transaction);

            await transaction.CommitAsync();

            await AuditLogAsync(kullaniciId, "personeller", id.ToString(), "Sil",
                JsonSerializer.Serialize(new { Aktif = true }),
                JsonSerializer.Serialize(new { Aktif = false }),
                "Personel pasifize edildi");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Hareket>> HareketleriGetirAsync(Guid? personelId = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = @"SELECT h.*, p.adi_soyadi AS AdiSoyadi
                    FROM hareketler h
                    LEFT JOIN personeller p ON h.h_personel_id = p.id
                    WHERE 1=1";

        if (personelId.HasValue) sql += " AND h.h_personel_id = @PersonelId";
        sql += " ORDER BY h.tarih DESC";

        var result = await conn.QueryAsync<Hareket>(sql, new { PersonelId = personelId });
        return result.ToList();
    }

    public async Task PersonelAktiflestirAsync(Guid id, Guid kullaniciId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            await conn.ExecuteAsync(
                "UPDATE personeller SET aktif = true WHERE id = @Id",
                new { Id = id }, transaction);

            await conn.ExecuteAsync(
                @"INSERT INTO hareketler (id, h_personel_id, tarih, aciklama)
                  VALUES (@Id, @HPersonelId, @Tarih, @Aciklama)",
                new { Id = Guid.NewGuid(), HPersonelId = id, Tarih = DateTime.UtcNow, Aciklama = "Tekrar İşe Giriş" },
                transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AuditLogAsync(Guid? kullaniciId, string tabloAdi, string? kayitId, string islemTipi,
        object? eskiDeger, object? yeniDeger, string? aciklama = null, string? ipAdresi = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        string? kullaniciAdi = null;
        if (kullaniciId.HasValue)
        {
            kullaniciAdi = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT kullanici_adi FROM kullanicilar WHERE id = @Id",
                new { Id = kullaniciId });
        }

        string eskiDegerJson = eskiDeger == null ? "null" : (eskiDeger is string s ? s : JsonSerializer.Serialize(eskiDeger));
        string yeniDegerJson = yeniDeger == null ? "null" : (yeniDeger is string s2 ? s2 : JsonSerializer.Serialize(yeniDeger));

        await conn.ExecuteAsync(
            @"INSERT INTO audit_log (id, kullanici_id, kullanici_adi, tablo_adi, kayit_id, islem_tipi, eski_deger, yeni_deger, ip_adresi, aciklama, tarih)
              VALUES (@Id, @KullaniciId, @KullaniciAdi, @TabloAdi, @KayitId, @IslemTipi, @EskiDeger::jsonb, @YeniDeger::jsonb, @IpAdresi, @Aciklama, @Tarih)",
            new
            {
                Id = Guid.NewGuid(),
                KullaniciId = kullaniciId,
                KullaniciAdi = kullaniciAdi,
                TabloAdi = tabloAdi,
                KayitId = kayitId,
                IslemTipi = islemTipi,
                EskiDeger = eskiDegerJson,
                YeniDeger = yeniDegerJson,
                IpAdresi = ipAdresi,
                Aciklama = aciklama,
                Tarih = DateTime.UtcNow
            });
    }

    public async Task<List<string>> AdminMailleriniGetirAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        var sql = "SELECT email FROM kullanicilar WHERE rol IN ('Admin', 'SuperAdmin') AND email IS NOT NULL";
        var result = await conn.QueryAsync<string>(sql);
        return result.ToList();
    }

    public async Task SistemiSifirlaAsync(Guid superAdminId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            // 1. Bağımlı ve ana tabloları CASCADE ile güvenli bir şekilde temizle
            // Tüm log, hareket ve geçici tabloları temizliyoruz.
            await conn.ExecuteAsync("TRUNCATE TABLE audit_log, hareketler, personeller, parola_yenileme, kullanici_degisiklik_log RESTART IDENTITY CASCADE", null, transaction);
            
            // 2. Admin dışındaki kullanıcıları sil
            // Önce adminin şantiye bağlantısını koparalım (temiz bir başlangıç için)
            await conn.ExecuteAsync("UPDATE kullanicilar SET santiye_id = NULL WHERE id = @Id", new { Id = superAdminId }, transaction);
            await conn.ExecuteAsync("DELETE FROM kullanicilar WHERE id != @Id", new { Id = superAdminId }, transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task BildirimEkleAsync(string mesaj, Guid tetikleyenKullaniciId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        
        await conn.ExecuteAsync(@"
            INSERT INTO bildirimler (mesaj, tetikleyen_kullanici_id, tarih) 
            VALUES (@Mesaj, @TetikleyenId, @Tarih)", 
            new { Mesaj = mesaj, TetikleyenId = tetikleyenKullaniciId, Tarih = DateTime.UtcNow });
            
        // Postgres pub/sub - NOTIFY komutu parametre desteklemez. 
        // Dapper bazen string içindeki karakterleri parametre sanabildiği için doğrudan NpgsqlCommand kullanıyoruz.
        var payload = $"{tetikleyenKullaniciId}|{mesaj}".Replace("'", "''");
        using var cmd = new NpgsqlCommand($"NOTIFY personel_bildirim, '{payload}';", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SistemBildirimiGonderAsync(string payload)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        
        var safePayload = payload.Replace("'", "''");
        using var cmd = new NpgsqlCommand($"NOTIFY personel_bildirim, '{safePayload}';", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Bildirim>> GetSonBildirimlerAsync(Guid kullaniciId, int limit = 20)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<Bildirim>(@"
            SELECT b.*, COALESCE(bd.okundu_mu, FALSE) as okundu_mu 
            FROM bildirimler b
            LEFT JOIN bildirim_durumlari bd ON b.id = bd.bildirim_id AND bd.kullanici_id = @KullaniciId
            WHERE bd.silindi_mi IS NOT TRUE
            ORDER BY b.tarih DESC LIMIT @Limit", 
            new { KullaniciId = kullaniciId, Limit = limit })).ToList();
    }

    public async Task OkunmadiIseOkunduYapAsync(int bildirimId, Guid kullaniciId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO bildirim_durumlari (kullanici_id, bildirim_id, okundu_mu) 
            VALUES (@KullaniciId, @BildirimId, TRUE)
            ON CONFLICT (kullanici_id, bildirim_id) DO UPDATE SET okundu_mu = TRUE", 
            new { KullaniciId = kullaniciId, BildirimId = bildirimId });
    }

    public async Task BildirimSilAsync(int bildirimId, Guid kullaniciId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO bildirim_durumlari (kullanici_id, bildirim_id, silindi_mi) 
            VALUES (@KullaniciId, @BildirimId, TRUE)
            ON CONFLICT (kullanici_id, bildirim_id) DO UPDATE SET silindi_mi = TRUE", 
            new { KullaniciId = kullaniciId, BildirimId = bildirimId });
    }

    public async Task TumBildirimleriSilAsync(Guid kullaniciId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO bildirim_durumlari (kullanici_id, bildirim_id, silindi_mi)
            SELECT @KullaniciId, id, TRUE FROM bildirimler
            ON CONFLICT (kullanici_id, bildirim_id) DO UPDATE SET silindi_mi = TRUE", 
            new { KullaniciId = kullaniciId });
    }
    
    public async Task StartListeningNotifications(Action<string, string> onNotificationReceived, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);
                
                conn.Notification += (o, e) => 
                {
                    if (e.Channel == "personel_bildirim" && !string.IsNullOrEmpty(e.Payload))
                    {
                        var parts = e.Payload.Split('|', 2);
                        if (parts.Length == 2)
                        {
                            onNotificationReceived?.Invoke(parts[0], parts[1]);
                        }
                        else
                        {
                            onNotificationReceived?.Invoke(e.Payload, string.Empty);
                        }
                    }
                };
                
                await using var cmd = new NpgsqlCommand("LISTEN personel_bildirim;", conn);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                
                // Bağlantı açık olduğu sürece bekle
                while (!cancellationToken.IsCancellationRequested && conn.State == System.Data.ConnectionState.Open)
                {
                    await conn.WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                // Bağlantı hatası durumunda 5 saniye bekle ve tekrar dene
                System.Diagnostics.Debug.WriteLine($"Bildirim dinleyici hatası: {ex.Message}");
                try { await Task.Delay(5000, cancellationToken); } catch { break; }
            }
        }
    }

    #region Cihaz İşlemleri

    public async Task<List<Cihaz>> CihazlariGetirAsync(CihazTuru? tur = null, Guid? santiyeId = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = @"SELECT c.*, s.adi AS SantiyeAdi, s.kod AS SantiyeKod, p.adi_soyadi AS ZimmetliPersonelAd, c.not_text AS ""Not""
                    FROM cihazlar c
                    LEFT JOIN santiyeler s ON c.santiye_id = s.id
                    LEFT JOIN personeller p ON c.zimmetli_personel_id = p.id
                    WHERE 1=1";

        if (tur.HasValue) sql += " AND c.tur = @Tur";
        if (santiyeId.HasValue) sql += " AND c.santiye_id = @SantiyeId";

        sql += " ORDER BY c.cihaz_adi";

        var result = await conn.QueryAsync<Cihaz>(sql, new { Tur = (int?)tur, SantiyeId = santiyeId });
        return result.ToList();
    }

    public async Task CihazEkleAsync(Cihaz cihaz)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync(@"
            INSERT INTO cihazlar (id, seri_no, cihaz_adi, marka, model, sahip_firma, not_text, tur, santiye_id, durum, son_islem_tarihi, foto_path, zimmetli_personel_id, zimmet_tarihi)
            VALUES (@Id, @SeriNo, @CihazAdi, @Marka, @Model, @SahipFirma, @Not, @Tur, @SantiyeId, @Durum, @SonIslemTarihi, @FotoPath, @ZimmetliPersonelId, @ZimmetTarihi)",
            cihaz);
    }

    public async Task CihazGuncelleAsync(Cihaz cihaz)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync(@"
            UPDATE cihazlar SET 
                seri_no = @SeriNo, cihaz_adi = @CihazAdi, marka = @Marka, model = @Model, 
                sahip_firma = @SahipFirma, not_text = @Not, tur = @Tur, 
                santiye_id = @SantiyeId, durum = @Durum, son_islem_tarihi = @SonIslemTarihi,
                foto_path = @FotoPath, zimmetli_personel_id = @ZimmetliPersonelId, zimmet_tarihi = @ZimmetTarihi
            WHERE id = @Id",
            cihaz);
    }

    public async Task ZimmetleAsync(Guid cihazId, Guid personelId, DateTime tarih, string aciklama, Guid kullaniciId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();
        try
        {
            // 1. Cihazı güncelle
            await conn.ExecuteAsync(@"
                UPDATE cihazlar SET 
                    zimmetli_personel_id = @PersonelId, 
                    zimmet_tarihi = @Tarih,
                    durum = 'Zimmetli',
                    son_islem_tarihi = @Tarih
                WHERE id = @CihazId",
                new { CihazId = cihazId, PersonelId = personelId, Tarih = tarih }, trans);

            // 2. Özel Zimmet Geçmişine ekle
            await conn.ExecuteAsync(@"
                INSERT INTO cihaz_zimmet_gecmisi (cihaz_id, personel_id, zimmet_tarihi, aciklama)
                VALUES (@CihazId, @PersonelId, @Tarih, @Aciklama)",
                new { CihazId = cihazId, PersonelId = personelId, Tarih = tarih, Aciklama = aciklama }, trans);

            // 3. Genel Hareket Tablosuna ekle (Geçmiş ekranında görünmesi için)
            await conn.ExecuteAsync(@"
                INSERT INTO cihaz_hareketleri (id, cihaz_id, personel_id, tarih, kullanici_id, aciklama, islem_turu)
                VALUES (@Id, @CihazId, @PersonelId, @Tarih, @KullaniciId, @Aciklama, 'Zimmet')",
                new { 
                    Id = Guid.NewGuid(), 
                    CihazId = cihazId, 
                    PersonelId = personelId, 
                    Tarih = tarih, 
                    KullaniciId = kullaniciId, 
                    Aciklama = aciklama 
                }, trans);

            await trans.CommitAsync();
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }

    public async Task IadeAlAsync(Guid cihazId, DateTime tarih, string aciklama, Guid kullaniciId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();
        try
        {
            // 1. Mevcut zimmet bilgisini al (geçmiş kaydını kapatmak için)
            var currentZimmet = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT zimmetli_personel_id, zimmet_tarihi FROM cihazlar WHERE id = @CihazId",
                new { CihazId = cihazId }, trans);

            if (currentZimmet is IDictionary<string, object> dict && dict.TryGetValue("zimmetli_personel_id", out var zimmetliIdObj) && zimmetliIdObj != null)
            {
                Guid pId = zimmetliIdObj is Guid guid ? guid : Guid.Empty;
                if (pId == Guid.Empty) Guid.TryParse(zimmetliIdObj.ToString(), out pId);

                // 2. Özel Zimmet Geçmiş kaydını iade tarihi ile güncelle
                await conn.ExecuteAsync(@"
                    UPDATE cihaz_zimmet_gecmisi SET iade_tarihi = @Tarih, aciklama = aciklama || ' | İade: ' || @Aciklama
                    WHERE cihaz_id = @CihazId AND personel_id = @PersonelId AND iade_tarihi IS NULL",
                    new { CihazId = cihazId, PersonelId = pId, Tarih = tarih, Aciklama = aciklama }, trans);

                // 3. Genel Hareket Tablosuna ekle (Geçmiş ekranında görünmesi için)
                await conn.ExecuteAsync(@"
                    INSERT INTO cihaz_hareketleri (id, cihaz_id, personel_id, tarih, kullanici_id, aciklama, islem_turu)
                    VALUES (@Id, @CihazId, @PersonelId, @Tarih, @KullaniciId, @Aciklama, 'İade')",
                    new { 
                        Id = Guid.NewGuid(), 
                        CihazId = cihazId, 
                        PersonelId = pId, 
                        Tarih = tarih, 
                        KullaniciId = kullaniciId, 
                        Aciklama = aciklama 
                    }, trans);
            }

            // 4. Cihazı boşa çıkar
            await conn.ExecuteAsync(@"
                UPDATE cihazlar SET 
                    zimmetli_personel_id = NULL, 
                    zimmet_tarihi = NULL,
                    durum = 'Boşta',
                    son_islem_tarihi = @Tarih
                WHERE id = @CihazId",
                new { CihazId = cihazId, Tarih = tarih }, trans);

            await trans.CommitAsync();
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }

    public async Task CihazSilAsync(Guid id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM cihazlar WHERE id = @Id", new { Id = id });
        await conn.ExecuteAsync("DELETE FROM cihaz_hareketleri WHERE cihaz_id = @Id", new { Id = id });
    }

    public async Task CihazHareketKaydetAsync(CihazHareket hareket)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();

        try
        {
            // Hareketi kaydet
            await conn.ExecuteAsync(@"
                INSERT INTO cihaz_hareketleri (id, cihaz_id, nereden_santiye_id, nereye_santiye_id, tarih, kullanici_id, aciklama, islem_turu)
                VALUES (@Id, @CihazId, @NeredenSantiyeId, @NereyeSantiyeId, @Tarih, @KullaniciId, @Aciklama, @IslemTuru)",
                hareket, trans);

            // Cihazın durumunu ve konumunu güncelle
            await conn.ExecuteAsync(@"
                UPDATE cihazlar SET 
                    santiye_id = @NereyeSantiyeId, 
                    durum = @IslemTuru, 
                    son_islem_tarihi = @Tarih
                WHERE id = @CihazId",
                new { hareket.NereyeSantiyeId, hareket.IslemTuru, hareket.Tarih, hareket.CihazId }, trans);

            await trans.CommitAsync();

            try
            {
                using var conn2 = CreateConnection();
                await conn2.OpenAsync();
                var cihaz = (await conn2.QueryAsync<Cihaz>("SELECT * FROM cihazlar WHERE id = @Id", new { Id = hareket.CihazId })).FirstOrDefault();
                var santiye = (await conn2.QueryAsync<Santiye>("SELECT * FROM santiyeler WHERE id = @Id", new { Id = hareket.NereyeSantiyeId })).FirstOrDefault();
                if (cihaz != null && santiye != null)
                {
                    string msg = $"{cihaz.CihazAdi} ({cihaz.SeriNo}) cihazı {santiye.Adi} şantiyesine sevk edildi.";
                    await conn2.ExecuteAsync("INSERT INTO bildirimler (mesaj, tetikleyen_kullanici_id, tarih) VALUES (@Mesaj, @KullaniciId, @Tarih)", 
                        new { Mesaj = msg, KullaniciId = hareket.KullaniciId, Tarih = DateTime.Now });
                    
                    // Canlı bildirim gönder (Başında KullaniciId olmalı ki dinleyici tanısın)
                    await conn2.ExecuteAsync($"NOTIFY personel_bildirim, '{hareket.KullaniciId}|{msg}';");
                }
            }
            catch { }
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }

    public async Task<List<CihazHareket>> CihazGecmisiGetirAsync(Guid cihazId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        return (await conn.QueryAsync<CihazHareket>(@"
            -- 1. Ölçüm Cihazı Hareketleri
            SELECT h.id, h.cihaz_id, h.personel_id, h.nereden_santiye_id, h.nereye_santiye_id, h.tarih, h.kullanici_id, h.aciklama, h.islem_turu,
                   s1.adi AS NeredenSantiyeAdi, s2.adi AS NereyeSantiyeAdi, 
                   k.kullanici_adi AS KullaniciAdi,
                   c.seri_no AS CihazSeriNo, c.cihaz_adi AS CihazAdi, p.adi_soyadi AS PersonelAd
            FROM cihaz_hareketleri h
            LEFT JOIN santiyeler s1 ON h.nereden_santiye_id = s1.id
            LEFT JOIN santiyeler s2 ON h.nereye_santiye_id = s2.id
            LEFT JOIN kullanicilar k ON h.kullanici_id = k.id
            LEFT JOIN cihazlar c ON h.cihaz_id = c.id
            LEFT JOIN personeller p ON h.personel_id = p.id
            WHERE h.cihaz_id = @CihazId

            UNION ALL

            -- 2. Ofis Cihazı Zimmet Hareketleri
            SELECT zg.cihaz_id as id, zg.cihaz_id, zg.personel_id, NULL as nereden_santiye_id, NULL as nereye_santiye_id, zg.zimmet_tarihi as tarih, NULL as kullanici_id, zg.aciklama, 'Zimmet' as islem_turu,
                   NULL AS NeredenSantiyeAdi, NULL AS NereyeSantiyeAdi, 
                   'Sistem' AS KullaniciAdi,
                   c.seri_no AS CihazSeriNo, c.cihaz_adi AS CihazAdi, p.adi_soyadi AS PersonelAd
            FROM cihaz_zimmet_gecmisi zg
            LEFT JOIN cihazlar c ON zg.cihaz_id = c.id
            LEFT JOIN personeller p ON zg.personel_id = p.id
            WHERE zg.cihaz_id = @CihazId

            UNION ALL

            -- 3. Ofis Cihazı İade Hareketleri
            SELECT zg.cihaz_id as id, zg.cihaz_id, zg.personel_id, NULL as nereden_santiye_id, NULL as nereye_santiye_id, zg.iade_tarihi as tarih, NULL as kullanici_id, zg.iade_aciklamasi as aciklama, 'İade' as islem_turu,
                   NULL AS NeredenSantiyeAdi, NULL AS NereyeSantiyeAdi, 
                   'Sistem' AS KullaniciAdi,
                   c.seri_no AS CihazSeriNo, c.cihaz_adi AS CihazAdi, p.adi_soyadi AS PersonelAd
            FROM cihaz_zimmet_gecmisi zg
            LEFT JOIN cihazlar c ON zg.cihaz_id = c.id
            LEFT JOIN personeller p ON zg.personel_id = p.id
            WHERE zg.cihaz_id = @CihazId AND zg.iade_tarihi IS NOT NULL

            ORDER BY tarih DESC", new { CihazId = cihazId })).ToList();
    }

    public async Task<List<CihazHareket>> TumCihazHareketleriniGetirAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        // Hem cihaz_hareketleri (Ölçüm sevk) hem de cihaz_zimmet_gecmisi (Ofis zimmet) verilerini birleştiriyoruz
        return (await conn.QueryAsync<CihazHareket>(@"
            -- 1. Ölçüm Cihazı Hareketleri (Şantiye Sevkleri)
            SELECT h.id, h.cihaz_id, h.personel_id, h.nereden_santiye_id, h.nereye_santiye_id, h.tarih, h.kullanici_id, h.aciklama, h.islem_turu,
                   s1.adi AS NeredenSantiyeAdi, s2.adi AS NereyeSantiyeAdi, 
                   s1.kod AS NeredenSantiyeKod, s2.kod AS NereyeSantiyeKod,
                   k.kullanici_adi AS KullaniciAdi,
                   c.seri_no AS CihazSeriNo, c.cihaz_adi AS CihazAdi, c.marka AS CihazMarka, c.model AS CihazModel,
                   c.ozellik AS CihazOzellik, c.not_text AS CihazNot,
                   c.tur AS CihazTuru,
                   p.adi_soyadi AS PersonelAd
            FROM cihaz_hareketleri h
            LEFT JOIN santiyeler s1 ON h.nereden_santiye_id = s1.id
            LEFT JOIN santiyeler s2 ON h.nereye_santiye_id = s2.id
            LEFT JOIN kullanicilar k ON h.kullanici_id = k.id
            LEFT JOIN cihazlar c ON h.cihaz_id = c.id
            LEFT JOIN personeller p ON h.personel_id = p.id

            UNION ALL

            -- 2. Ofis Cihazı Hareketleri (Zimmet Kayıtları)
            SELECT zg.cihaz_id as id, zg.cihaz_id, zg.personel_id, NULL as nereden_santiye_id, NULL as nereye_santiye_id, zg.zimmet_tarihi as tarih, NULL as kullanici_id, zg.aciklama, 'Zimmet' as islem_turu,
                   NULL AS NeredenSantiyeAdi, NULL AS NereyeSantiyeAdi, NULL AS NeredenSantiyeKod, NULL AS NereyeSantiyeKod,
                   'Sistem' AS KullaniciAdi,
                   c.seri_no AS CihazSeriNo, c.cihaz_adi AS CihazAdi, c.marka AS CihazMarka, c.model AS CihazModel,
                   c.ozellik AS CihazOzellik, c.not_text AS CihazNot,
                   1 AS CihazTuru, -- Ofis
                   p.adi_soyadi AS PersonelAd
            FROM cihaz_zimmet_gecmisi zg
            LEFT JOIN cihazlar c ON zg.cihaz_id = c.id
            LEFT JOIN personeller p ON zg.personel_id = p.id

            UNION ALL

            -- 3. Ofis Cihazı İade Hareketleri
            SELECT zg.cihaz_id as id, zg.cihaz_id, zg.personel_id, NULL as nereden_santiye_id, NULL as nereye_santiye_id, zg.iade_tarihi as tarih, NULL as kullanici_id, zg.iade_aciklamasi as aciklama, 'İade' as islem_turu,
                   NULL AS NeredenSantiyeAdi, NULL AS NereyeSantiyeAdi, NULL AS NeredenSantiyeKod, NULL AS NereyeSantiyeKod,
                   'Sistem' AS KullaniciAdi,
                   c.seri_no AS CihazSeriNo, c.cihaz_adi AS CihazAdi, c.marka AS CihazMarka, c.model AS CihazModel,
                   c.ozellik AS CihazOzellik, c.not_text AS CihazNot,
                   1 AS CihazTuru, -- Ofis
                   p.adi_soyadi AS PersonelAd
            FROM cihaz_zimmet_gecmisi zg
            LEFT JOIN cihazlar c ON zg.cihaz_id = c.id
            LEFT JOIN personeller p ON zg.personel_id = p.id
            WHERE zg.iade_tarihi IS NOT NULL

            ORDER BY tarih DESC", commandTimeout: 120)).ToList();
    }

    public async Task SantiyeAktiflestirAsync(Guid id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("UPDATE santiyeler SET aktif = true WHERE id = @Id", new { Id = id });
    }

    #endregion

    public async Task<string> GenerateBackupSqlAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var tables = new[]
        {
            // 1. Lookup tabloları (bağımlılık yok)
            "bolumler", "gorevler", "uyruklar", "para_birimleri",
            "cihaz_adlari", "cihaz_markalari", "cihaz_modelleri", "cihaz_firmalari",
            // 2. Ana tablolar (sıralı bağımlılık)
            "santiyeler", "kullanicilar", "personeller", "cihazlar",
            // 3. İlişkili tablolar
            "cihaz_hareketleri", "cihaz_zimmet_gecmisi",
            "hareketler", "bildirimler", "bildirim_durumlari",
            "audit_log", "cihaz_lisanslari"
        };

        var sb = new StringBuilder();
        sb.AppendLine("-- Personel Takip Sistemi - Otomatik Veritabanı Yedeği");
        sb.AppendLine($"-- Tarih: {DateTime.Now}");
        sb.AppendLine($"-- Tablo Sayısı: {tables.Length}");
        sb.AppendLine();

        // Geri yükleme sırasında FK kısıtlamalarını ve tetikleyicileri devre dışı bırakmanın en güvenli yolu (Supabase uyumlu)
        sb.AppendLine("-- Veri bütünlüğü kontrollerini ve tetikleyicileri geçici olarak devre dışı bırak");
        sb.AppendLine("SET session_replication_role = 'replica';");
        sb.AppendLine();

        // Tüm tabloları temizle (CASCADE ile)
        sb.AppendLine("-- Tabloları temizle");
        foreach (var table in tables.Reverse())
            sb.AppendLine($"TRUNCATE TABLE {table} CASCADE;");
        sb.AppendLine();

        foreach (var table in tables)
        {
            try
            {
                var rows = (await conn.QueryAsync($"SELECT * FROM {table}")).ToList();
                if (rows.Count > 0)
                {
                    sb.AppendLine($"-- Table: {table} ({rows.Count} kayıt)");
                    foreach (var row in rows)
                    {
                        var fields = (IDictionary<string, object>)row;
                        var columns = string.Join(", ", fields.Keys);
                        var values = string.Join(", ", fields.Values.Select(FormatSqlValue));
                        sb.AppendLine($"INSERT INTO {table} ({columns}) VALUES ({values});");
                    }
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"-- Error backing up {table}: {ex.Message}");
            }
        }

        // Veri bütünlüğü kontrollerini ve tetikleyicileri yeniden etkinleştir
        sb.AppendLine("-- Veri bütünlüğü kontrollerini ve tetikleyicileri yeniden etkinleştir");
        sb.AppendLine("SET session_replication_role = 'origin';");
        sb.AppendLine();

        // Sequence'ları güncelle (SERIAL sütunlar için)
        sb.AppendLine("-- Sequence'ları güncelle");
        var serialTables = new[] { "bolumler", "gorevler", "uyruklar", "para_birimleri", "cihaz_adlari", "cihaz_markalari", "cihaz_modelleri", "cihaz_firmalari", "bildirimler" };
        foreach (var t in serialTables)
            sb.AppendLine($"SELECT setval(pg_get_serial_sequence('{t}', 'id'), COALESCE((SELECT MAX(id) FROM {t}), 0) + 1, false);");

        return sb.ToString();
    }

    private string FormatSqlValue(object value)
    {
        if (value == null || value == DBNull.Value) return "NULL";
        if (value is string s) return $"'{s.Replace("'", "''")}'";
        if (value is Guid g) return $"'{g}'";
        if (value is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
        if (value is bool b) return b ? "TRUE" : "FALSE";
        if (value is byte[] bytes) return $"E'\\\\x{BitConverter.ToString(bytes).Replace("-", "")}'";
        if (value is double || value is float || value is decimal || value is int || value is long)
            return value?.ToString()?.Replace(",", ".") ?? "0";
        
        return $"'{value?.ToString()?.Replace("'", "''") ?? ""}'";
    }

    public async Task RestoreBackupSqlAsync(string sql)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();
        try
        {
            // Dapper büyük SQL'leri tek seferde çalıştıramayabilir, NpgsqlCommand kullan
            using var cmd = new NpgsqlCommand(sql, conn, trans);
            cmd.CommandTimeout = 300; // 5 dakika timeout
            await cmd.ExecuteNonQueryAsync();
            await trans.CommitAsync();
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }

    public async Task<(string? licenseKey, string? demoData)> GetLicenseDataAsync(string hardwareId)
    {
        try
        {
            using var conn = CreateConnection();
            var result = await conn.QueryFirstOrDefaultAsync("SELECT license_key, demo_data FROM cihaz_lisanslari WHERE hardware_id = @id", new { id = hardwareId });
            if (result == null) return (null, null);
            return (result.license_key, result.demo_data);
        }
        catch { return (null, null); }
    }

    public async Task SaveLicenseDataAsync(string hardwareId, string? licenseKey, string? demoData)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.ExecuteAsync(@"
                INSERT INTO cihaz_lisanslari (hardware_id, license_key, demo_data, updated_at)
                VALUES (@id, @key, @demo, now())
                ON CONFLICT (hardware_id) DO UPDATE SET
                    license_key = EXCLUDED.license_key,
                    demo_data = EXCLUDED.demo_data,
                    updated_at = now()",
                new { id = hardwareId, key = licenseKey, demo = demoData });
        }
        catch { }
    }
}
