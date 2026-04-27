using System.Text.Json;
using Dapper;
using Npgsql;
using PersonelTakip.Models;

namespace PersonelTakip.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    static DatabaseService()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public DatabaseService()
    {
        _connectionString = AppConfiguration.Instance.Database.ConnectionString;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            
            // 1. Bağımsız Tablolar
            await conn.ExecuteAsync(@"
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

                CREATE TABLE IF NOT EXISTS bolumler (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS gorevler (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS uyruklar (id SERIAL PRIMARY KEY, adi TEXT UNIQUE NOT NULL);
                CREATE TABLE IF NOT EXISTS para_birimleri (id SERIAL PRIMARY KEY, adi VARCHAR(50) NOT NULL);
                
                CREATE TABLE IF NOT EXISTS cihaz_adlari (id SERIAL PRIMARY KEY, adi VARCHAR(100) NOT NULL);
                CREATE TABLE IF NOT EXISTS cihaz_markalari (id SERIAL PRIMARY KEY, adi VARCHAR(100) NOT NULL);
                CREATE TABLE IF NOT EXISTS cihaz_modelleri (id SERIAL PRIMARY KEY, adi VARCHAR(100) NOT NULL);
                CREATE TABLE IF NOT EXISTS cihaz_firmalari (id SERIAL PRIMARY KEY, adi VARCHAR(100) NOT NULL);
            ");

            // 2. Kullanıcılar ve Audit
            await conn.ExecuteAsync(@"
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
            ");

            // 3. Personeller ve Cihazlar
            await conn.ExecuteAsync(@"
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
                    sahip_firma TEXT,
                    not_text TEXT,
                    tur INTEGER,
                    santiye_id UUID REFERENCES santiyeler(id),
                    durum TEXT,
                    son_islem_tarihi TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS cihaz_hareketleri (
                    id UUID PRIMARY KEY,
                    cihaz_id UUID REFERENCES cihazlar(id),
                    nereden_santiye_id UUID REFERENCES santiyeler(id),
                    nereye_santiye_id UUID REFERENCES santiyeler(id),
                    tarih TIMESTAMP NOT NULL,
                    kullanici_id UUID REFERENCES kullanicilar(id),
                    aciklama TEXT,
                    islem_turu TEXT
                );
            ");

            // 4. Bildirimler
            await conn.ExecuteAsync(@"
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
                    PRIMARY KEY (kullanici_id, bildirim_id),
                    FOREIGN KEY (bildirim_id) REFERENCES bildirimler(id) ON DELETE CASCADE
                );
            ");

            // 5. Fonksiyon ve Tetikleyiciler
            await conn.ExecuteAsync(@"
                CREATE OR REPLACE FUNCTION fn_cihaz_hareket_bildirim()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_cihaz_adi TEXT;
                    v_seri_no TEXT;
                    v_santiye_kod TEXT;
                    v_mesaj TEXT;
                BEGIN
                    SELECT cihaz_adi, seri_no INTO v_cihaz_adi, v_seri_no FROM cihazlar WHERE id = NEW.cihaz_id;
                    SELECT kod INTO v_santiye_kod FROM santiyeler WHERE id = NEW.nereye_santiye_id;
                    IF v_santiye_kod IS NULL THEN v_santiye_kod := 'Merkez'; END IF;
                    v_mesaj := v_cihaz_adi || ' (' || v_seri_no || ') ' || v_santiye_kod || ' konumuna (' || NEW.islem_turu || ') transfer edildi.';
                    INSERT INTO bildirimler (mesaj, tetikleyen_kullanici_id, tarih) VALUES (v_mesaj, NEW.kullanici_id, NEW.tarih);
                    PERFORM pg_notify('personel_bildirim', NEW.kullanici_id || '|' || v_mesaj);
                    RETURN NEW;
                END; $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_cihaz_hareket_bildirim ON cihaz_hareketleri;
                CREATE TRIGGER trg_cihaz_hareket_bildirim
                AFTER INSERT ON cihaz_hareketleri
                FOR EACH ROW EXECUTE FUNCTION fn_cihaz_hareket_bildirim();
            ");

            // 6. Varsayılan Admin Kullanıcısı
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
            System.Diagnostics.Debug.WriteLine($"DB Init Error: {ex.Message}");
            return false;
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
        if (kullanici.SantiyeId.HasValue)
        {
            var sayi = await GetSantiyeKullaniciSayisiAsync(kullanici.SantiyeId.Value);
            if (sayi >= 1)
            {
                throw new InvalidOperationException("Bu şantiyeye atanmış bir kullanıcı zaten mevcut. Her şantiye için sadece tek bir kullanıcı tanımlanabilir.");
            }
        }

        using var conn = CreateConnection();
        await conn.OpenAsync();
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

                var tabloAdi = tablo.ToLower() switch
                {
                    "bolumler" => "bolumu",
                    "gorevler" => "gorevi",
                    "uyruklar" => "uyrugu",
                    "para_birimleri" => "para_birimi",
                    _ => tablo.ToLower().TrimEnd('r')
                };

                var kontrolSql = $"SELECT COUNT(*) FROM personeller WHERE {tabloAdi} = @Id";
                var sayi = await conn.ExecuteScalarAsync<int>(kontrolSql, new { Id = id });

                if (sayi > 0)
                {
                    throw new InvalidOperationException($"Bu kayıt {sayi} personelinin başvurusunda kullanıldığı için silinemez.");
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

        var sql = @"SELECT c.*, s.adi AS SantiyeAdi, s.kod AS SantiyeKod, c.not_text AS ""Not""
                    FROM cihazlar c
                    LEFT JOIN santiyeler s ON c.santiye_id = s.id
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
            INSERT INTO cihazlar (id, seri_no, cihaz_adi, marka, model, sahip_firma, not_text, tur, santiye_id, durum, son_islem_tarihi)
            VALUES (@Id, @SeriNo, @CihazAdi, @Marka, @Model, @SahipFirma, @Not, @Tur, @SantiyeId, @Durum, @SonIslemTarihi)",
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
                santiye_id = @SantiyeId, durum = @Durum, son_islem_tarihi = @SonIslemTarihi
            WHERE id = @Id",
            cihaz);
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
            SELECT h.*, s1.adi AS NeredenSantiyeAdi, s2.adi AS NereyeSantiyeAdi, 
                   s1.kod AS NeredenSantiyeKod, s2.kod AS NereyeSantiyeKod,
                   k.kullanici_adi AS KullaniciAdi,
                   c.seri_no AS CihazSeriNo, c.cihaz_adi AS CihazAdi
            FROM cihaz_hareketleri h
            LEFT JOIN santiyeler s1 ON h.nereden_santiye_id = s1.id
            LEFT JOIN santiyeler s2 ON h.nereye_santiye_id = s2.id
            LEFT JOIN kullanicilar k ON h.kullanici_id = k.id
            LEFT JOIN cihazlar c ON h.cihaz_id = c.id
            WHERE h.cihaz_id = @CihazId
            ORDER BY h.tarih DESC",
            new { CihazId = cihazId })).ToList();
    }

    public async Task<List<CihazHareket>> TumCihazHareketleriniGetirAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        return (await conn.QueryAsync<CihazHareket>(@"
            SELECT h.*, s1.adi AS NeredenSantiyeAdi, s2.adi AS NereyeSantiyeAdi, 
                   s1.kod AS NeredenSantiyeKod, s2.kod AS NereyeSantiyeKod,
                   k.kullanici_adi AS KullaniciAdi,
                   c.seri_no AS CihazSeriNo, c.cihaz_adi AS CihazAdi, c.marka AS CihazMarka, c.model AS CihazModel
            FROM cihaz_hareketleri h
            LEFT JOIN santiyeler s1 ON h.nereden_santiye_id = s1.id
            LEFT JOIN santiyeler s2 ON h.nereye_santiye_id = s2.id
            LEFT JOIN kullanicilar k ON h.kullanici_id = k.id
            LEFT JOIN cihazlar c ON h.cihaz_id = c.id
            ORDER BY h.tarih DESC")).ToList();
    }

    public async Task SantiyeAktiflestirAsync(Guid id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("UPDATE santiyeler SET aktif = true WHERE id = @Id", new { Id = id });
    }

    #endregion
}
