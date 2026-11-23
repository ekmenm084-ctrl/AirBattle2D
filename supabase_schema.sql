-- ====================================================================
-- AIRBATTLE2D: KÜRESEL LİDERLİK TABLOSU SUPABASE POSTGRESQL ŞEMASI
-- ====================================================================
-- Bu SQL kodunu Supabase Dashboard -> SQL Editor alanına yapıştırıp "RUN" butonuna basarak
-- veritabanı tablonuzu ve güvenlik politikalarını 5 saniyede oluşturabilirsiniz.

-- 1. Liderlik Tablosunu Oluştur
CREATE TABLE IF NOT EXISTS public.leaderboard (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    player_name TEXT NOT NULL,
    mode TEXT NOT NULL,              -- 'endless', 'time_attack', 'bullet_hell', 'boss_rush', 'hardcore'
    score INTEGER NOT NULL,          -- Dalga sayısı, imha sayısı veya puan
    time_seconds REAL DEFAULT 0,     -- Geçen süre (saniye)
    formatted_score TEXT DEFAULT '', -- Ekranda görünecek format (örn: 'DALGA 42', '02:15 SN')
    rank_title TEXT DEFAULT '',      -- Askeri unvan (örn: 'BAŞKOMUTAN AS')
    extra_info TEXT DEFAULT '',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT leaderboard_player_mode_key UNIQUE (player_name, mode)
);

-- 2. Hızlı Sıralama ve Arama İndeksleri
CREATE INDEX IF NOT EXISTS idx_leaderboard_mode_score 
ON public.leaderboard (mode, score DESC, time_seconds ASC);

-- 3. Row Level Security (RLS) Güvenlik Politikalarını Aktif Et
ALTER TABLE public.leaderboard ENABLE ROW LEVEL SECURITY;

-- 4. Okuma İzni (Herkes liderlik tablosunu okuyabilir)
DROP POLICY IF EXISTS "Public Leaderboard Read Access" ON public.leaderboard;
CREATE POLICY "Public Leaderboard Read Access"
ON public.leaderboard FOR SELECT
USING (true);

-- 5. Ekleme ve Güncelleme İzni (Oyun anonim API anahtarı ile yeni skor ekleyebilir / güncelleyebilir)
DROP POLICY IF EXISTS "Public Leaderboard Upsert Access" ON public.leaderboard;
CREATE POLICY "Public Leaderboard Upsert Access"
ON public.leaderboard FOR INSERT
WITH CHECK (true);

DROP POLICY IF EXISTS "Public Leaderboard Update Access" ON public.leaderboard;
CREATE POLICY "Public Leaderboard Update Access"
ON public.leaderboard FOR UPDATE
USING (true);

-- ====================================================================
-- AIRBATTLE2D: KULLANICI GİRİŞ & KAYIT SİSTEMİ (USERNAME + PASSWORD)
-- ====================================================================
-- Şifreler pgcrypto (bcrypt) ile sunucu tarafında güvenli şekilde hashlenir.
-- Düz metin şifre asla veritabanına yazılmaz ve tablo dışarıdan okunamaz.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 6. Pilot Hesapları Tablosu
CREATE TABLE IF NOT EXISTS public.pilot_accounts (
    id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    username TEXT NOT NULL UNIQUE,
    username_lower TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    pilot_level INTEGER DEFAULT 1,
    pilot_exp INTEGER DEFAULT 0,
    gold INTEGER DEFAULT 250,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_login_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_pilot_accounts_username_lower
ON public.pilot_accounts (username_lower);

-- RLS Aktif: Doğrudan SELECT/INSERT/UPDATE kapalı, tüm işlemler güvenli RPC ile yapılır
ALTER TABLE public.pilot_accounts ENABLE ROW LEVEL SECURITY;

-- 7. Kayıt Olma RPC Fonksiyonu (register_pilot)
CREATE OR REPLACE FUNCTION public.register_pilot(p_username TEXT, p_password TEXT)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, extensions
AS $$
DECLARE
    v_clean TEXT;
    v_lower TEXT;
    v_id UUID;
BEGIN
    v_clean := trim(COALESCE(p_username, ''));
    IF length(v_clean) < 3 OR length(v_clean) > 16 THEN
        RETURN jsonb_build_object('success', false, 'error', 'Kullanıcı adı 3 ile 16 karakter arasında olmalıdır!');
    END IF;

    IF length(COALESCE(p_password, '')) < 4 THEN
        RETURN jsonb_build_object('success', false, 'error', 'Şifre en az 4 karakter olmalıdır!');
    END IF;

    v_lower := lower(v_clean);

    IF EXISTS (SELECT 1 FROM public.pilot_accounts WHERE username_lower = v_lower) THEN
        RETURN jsonb_build_object('success', false, 'error', 'Bu kullanıcı adı zaten alınmış! Lütfen farklı bir ad seçin.');
    END IF;

    INSERT INTO public.pilot_accounts (username, username_lower, password_hash)
    VALUES (v_clean, v_lower, crypt(p_password, gen_salt('bf')))
    RETURNING id INTO v_id;

    RETURN jsonb_build_object(
        'success', true,
        'id', v_id::text,
        'username', v_clean,
        'pilot_level', 1,
        'gold', 250
    );
END;
$$;

-- 8. Giriş Yapma RPC Fonksiyonu (login_pilot)
CREATE OR REPLACE FUNCTION public.login_pilot(p_username TEXT, p_password TEXT)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, extensions
AS $$
DECLARE
    v_lower TEXT;
    v_acc RECORD;
BEGIN
    v_lower := lower(trim(COALESCE(p_username, '')));
    IF length(v_lower) < 3 THEN
        RETURN jsonb_build_object('success', false, 'error', 'Lütfen geçerli bir kullanıcı adı girin!');
    END IF;

    SELECT id, username, password_hash, pilot_level, gold
    INTO v_acc
    FROM public.pilot_accounts
    WHERE username_lower = v_lower;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'Kullanıcı bulunamadı! Önce Kayıt Ol sekmesinden hesap oluşturun.');
    END IF;

    IF v_acc.password_hash <> crypt(COALESCE(p_password, ''), v_acc.password_hash) THEN
        RETURN jsonb_build_object('success', false, 'error', 'Hatalı şifre! Lütfen şifrenizi kontrol edin.');
    END IF;

    UPDATE public.pilot_accounts
    SET last_login_at = NOW()
    WHERE id = v_acc.id;

    RETURN jsonb_build_object(
        'success', true,
        'id', v_acc.id::text,
        'username', v_acc.username,
        'pilot_level', v_acc.pilot_level,
        'gold', v_acc.gold
    );
END;
$$;

-- 9. Anonim ve Giriş Yapmış İstemcilere RPC Çalıştırma İzni
GRANT EXECUTE ON FUNCTION public.register_pilot(TEXT, TEXT) TO anon, authenticated;
GRANT EXECUTE ON FUNCTION public.login_pilot(TEXT, TEXT) TO anon, authenticated;

-- ====================================================================
-- 10. KAYITLI KULLANICI REKORLARINI SKOR TABLOSUNA GÜVENLİ AKTARMA
-- ====================================================================
-- Liderlik tablosunu pilot_accounts hesabına bağla
ALTER TABLE public.leaderboard
ADD COLUMN IF NOT EXISTS account_id UUID REFERENCES public.pilot_accounts(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_leaderboard_account_id
ON public.leaderboard (account_id);

-- Kayıtlı / giriş yapmış oyuncunun rekorunu yalnızca daha yüksekse güncelleyen RPC
CREATE OR REPLACE FUNCTION public.submit_pilot_score(
    p_username TEXT,
    p_mode TEXT,
    p_score INTEGER,
    p_time_seconds REAL DEFAULT 0,
    p_formatted_score TEXT DEFAULT '',
    p_rank_title TEXT DEFAULT '',
    p_extra_info TEXT DEFAULT ''
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, extensions
AS $$
DECLARE
    v_clean TEXT;
    v_lower TEXT;
    v_acc_id UUID;
    v_acc_name TEXT;
BEGIN
    v_clean := trim(COALESCE(p_username, ''));
    IF length(v_clean) < 1 THEN
        RETURN jsonb_build_object('success', false, 'error', 'Geçersiz kullanıcı adı');
    END IF;

    IF COALESCE(p_score, 0) <= 0 AND COALESCE(p_time_seconds, 0) <= 0 THEN
        RETURN jsonb_build_object('success', false, 'error', 'Skor sıfırdan büyük olmalıdır');
    END IF;

    v_lower := lower(v_clean);

    -- Hesap pilot_accounts tablosunda kayıtlıysa canonical kullanıcı adını ve account_id'yi bağla
    SELECT id, username
    INTO v_acc_id, v_acc_name
    FROM public.pilot_accounts
    WHERE username_lower = v_lower
    LIMIT 1;

    IF v_acc_name IS NOT NULL THEN
        v_clean := v_acc_name;
    END IF;

    INSERT INTO public.leaderboard (
        player_name, mode, score, time_seconds, formatted_score, rank_title, extra_info, account_id, updated_at
    )
    VALUES (
        v_clean, p_mode, p_score, COALESCE(p_time_seconds, 0), COALESCE(p_formatted_score, ''),
        COALESCE(p_rank_title, ''), COALESCE(p_extra_info, ''), v_acc_id, NOW()
    )
    ON CONFLICT (player_name, mode) DO UPDATE SET
        score           = EXCLUDED.score,
        time_seconds    = EXCLUDED.time_seconds,
        formatted_score = EXCLUDED.formatted_score,
        rank_title      = EXCLUDED.rank_title,
        extra_info      = EXCLUDED.extra_info,
        account_id      = COALESCE(EXCLUDED.account_id, public.leaderboard.account_id),
        updated_at      = NOW()
    WHERE EXCLUDED.score > public.leaderboard.score
       OR (EXCLUDED.score = public.leaderboard.score AND EXCLUDED.time_seconds > public.leaderboard.time_seconds);

    RETURN jsonb_build_object('success', true, 'player_name', v_clean, 'mode', p_mode, 'score', p_score);
END;
$$;

GRANT EXECUTE ON FUNCTION public.submit_pilot_score(TEXT, TEXT, INTEGER, REAL, TEXT, TEXT, TEXT) TO anon, authenticated;
