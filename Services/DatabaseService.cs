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
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Kullanici?> KullaniciGirisAsync(string kullaniciAdi, string sifre)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var kullanici = await conn.QueryFirstOrDefaultAsync<Kullanici>(
            @"SELECT k.id, k.kullanici_adi, k.email, k.sifre_hash AS SifreHash, k.rol, k.santiye_id, k.aktif, k.son_giris, k.created_at, s.adi AS santiye_adi
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

    public async Task<List<Kullanici>> KullanicilariGetirAsync(Guid? santiyeId = null, string? rol = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = @"SELECT k.*, s.adi AS SantiyeAdi
                    FROM kullanicilar k
                    LEFT JOIN santiyeler s ON k.santiye_id = s.id
                    WHERE k.aktif = true";

        if (santiyeId.HasValue) sql += " AND k.santiye_id = @SantiyeId";
        if (!string.IsNullOrEmpty(rol)) sql += " AND k.rol = @Rol";

        sql += " ORDER BY k.kullanici_adi";

        var result = await conn.QueryAsync<Kullanici>(sql, new { SantiyeId = santiyeId, Rol = rol });
        return result.ToList();
    }

    public async Task<Guid> KullaniciOlusturAsync(Kullanici kullanici, string sifre)
    {
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

    public async Task<int> LookupOlusturAsync(string tablo, string adi)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var id = await conn.QueryFirstAsync<int>(
            $@"INSERT INTO {tablo} (adi) VALUES (@Adi); SELECT lastval();",
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
}
