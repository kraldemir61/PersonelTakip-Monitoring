-- ================================================
-- PERSONEL TAKİP SİSTEMİ - VERİTABANI ŞEMASI
-- Supabase PostgreSQL
-- ================================================

-- 1. ŞANTİYELER
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

-- 2. KULLANICILAR
CREATE TABLE IF NOT EXISTS kullanicilar (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    kullanici_adi TEXT UNIQUE NOT NULL,
    email TEXT UNIQUE NOT NULL,
    sifre_hash TEXT NOT NULL,
    rol TEXT CHECK (rol IN ('Admin', 'User')) NOT NULL DEFAULT 'User',
    santiye_id UUID REFERENCES santiyeler(id),
    aktif BOOLEAN DEFAULT true,
    son_giris TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT now()
);

-- 3. PAROLA YENİLEME
CREATE TABLE IF NOT EXISTS parola_yenileme (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    kullanici_id UUID REFERENCES kullanicilar(id) NOT NULL,
    email TEXT NOT NULL,
    token TEXT UNIQUE NOT NULL,
    token_suresi TIMESTAMPTZ NOT NULL,
    kullanildi BOOLEAN DEFAULT false,
    olusturma_tarihi TIMESTAMPTZ DEFAULT now()
);

-- 4. BÖLÜMLER (Lookup)
CREATE TABLE IF NOT EXISTS bolumler (
    id SERIAL PRIMARY KEY,
    adi TEXT UNIQUE NOT NULL
);

-- 5. GÖREVLER (Lookup)
CREATE TABLE IF NOT EXISTS gorevler (
    id SERIAL PRIMARY KEY,
    adi TEXT UNIQUE NOT NULL
);

-- 6. UYRUKLAR (Lookup)
CREATE TABLE IF NOT EXISTS uyruklar (
    id SERIAL PRIMARY KEY,
    adi TEXT UNIQUE NOT NULL
);

-- 7. PERSONELLER
CREATE TABLE IF NOT EXISTS personeller (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    santiye_id UUID REFERENCES santiyeler(id),
    adi_soyadi TEXT NOT NULL,
    bolumu INTEGER REFERENCES bolumler(id),
    gorevi INTEGER REFERENCES gorevler(id),
    uyrugu INTEGER REFERENCES uyruklar(id),
    para_birimi TEXT,
    ise_giris_tarihi DATE,
    telefon_numarasi TEXT,
    maas NUMERIC(15, 2),
    aktif BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT now()
);

-- 8. HAREKETLER
CREATE TABLE IF NOT EXISTS hareketler (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    h_personel_id UUID REFERENCES personeller(id) ON DELETE CASCADE,
    tarih TIMESTAMPTZ DEFAULT now(),
    aciklama TEXT
);

-- 9. AUDIT LOG
CREATE TABLE IF NOT EXISTS audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    kullanici_id UUID REFERENCES kullanicilar(id),
    kullanici_adi TEXT,
    tablo_adi TEXT NOT NULL,
    kayit_id TEXT,
    islem_tipi TEXT CHECK (islem_tipi IN ('Ekle', 'Guncelle', 'Sil', 'Giris', 'Cikis', 'RolDegistir')) NOT NULL,
    eski_deger JSONB,
    yeni_deger JSONB,
    ip_adresi TEXT,
    aciklama TEXT,
    tarih TIMESTAMPTZ DEFAULT now()
);

-- 10. KULLANICI DEĞİŞİKLİK LOG
CREATE TABLE IF NOT EXISTS kullanici_degisiklik_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_kullanici_id UUID REFERENCES kullanicilar(id) NOT NULL,
    hedef_kullanici_id UUID REFERENCES kullanicilar(id) NOT NULL,
    onceki_rol TEXT NOT NULL,
    yeni_rol TEXT NOT NULL,
    aciklama TEXT,
    tarih TIMESTAMPTZ DEFAULT now(),
    ip_adresi TEXT
);

-- ================================================
-- INDEXLER
-- ================================================

CREATE INDEX IF NOT EXISTS IX_personeller_santiye ON personeller(santiye_id);
CREATE INDEX IF NOT EXISTS IX_personeller_aktif ON personeller(aktif);
CREATE INDEX IF NOT EXISTS IX_personeller_adi_soyadi ON personeller(adi_soyadi);
CREATE INDEX IF NOT EXISTS IX_kullanicilar_email ON kullanicilar(email);
CREATE INDEX IF NOT EXISTS IX_kullanicilar_santiye ON kullanicilar(santiye_id);
CREATE INDEX IF NOT EXISTS IX_hareketler_personel ON hareketler(h_personel_id);
CREATE INDEX IF NOT EXISTS IX_audit_log_kullanici ON audit_log(kullanici_id);
CREATE INDEX IF NOT EXISTS IX_audit_log_tarih ON audit_log(tarih DESC);
CREATE INDEX IF NOT EXISTS IX_parola_yenileme_token ON parola_yenileme(token);

-- ================================================
-- RLS (Row Level Security) POLİCY'LERİ
-- ================================================

-- Kullanıcılar için RLS
ALTER TABLE kullanicilar ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Admin can see all users" ON kullanicilar
    FOR SELECT USING ( EXISTS (
        SELECT 1 FROM kullanicilar k
        WHERE k.id = current_setting('app.current_user_id', true)::uuid
        AND k.rol = 'Admin'
    ) );

CREATE POLICY "Users can see own data" ON kullanicilar
    FOR SELECT USING ( id = current_setting('app.current_user_id', true)::uuid );

-- Personeller için RLS
ALTER TABLE personeller ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Admin can see all personnel" ON personeller
    FOR SELECT USING ( EXISTS (
        SELECT 1 FROM kullanicilar k
        WHERE k.id = current_setting('app.current_user_id', true)::uuid
        AND k.rol = 'Admin'
    ) );

CREATE POLICY "User can see own santiye personnel" ON personeller
    FOR SELECT USING (
        santiye_id = (
            SELECT santiye_id FROM kullanicilar
            WHERE id = current_setting('app.current_user_id', true)::uuid
        )
    );

-- ================================================
-- FONKSİYONLAR
-- ================================================

-- Audit log için otomatik tetikleyici fonksiyonu
CREATE OR REPLACE FUNCTION public.log_audit()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO audit_log (kullanici_id, tablo_adi, kayit_id, islem_tipi, eski_deger, yeni_deger)
    VALUES (
        current_setting('app.current_user_id', true)::uuid,
        TG_TABLE_NAME,
        COALESCE(NEW.id::text, OLD.id::text),
        TG_OP,
        CASE WHEN TG_OP = 'DELETE' THEN row_to_json(OLD) ELSE NULL END,
        CASE WHEN TG_OP IN ('INSERT', 'UPDATE') THEN row_to_json(NEW) ELSE NULL END
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Son giriş tarihini güncelleyen fonksiyon
CREATE OR REPLACE FUNCTION public.update_son_giris()
RETURNS TRIGGER AS $$
BEGIN
    NEW.son_giris = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;