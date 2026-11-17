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
-- İsteğe Bağlı: İlk Açılış İçin Örnek As Pilotlar (Test Verisi)
-- ====================================================================
INSERT INTO public.leaderboard (player_name, mode, score, time_seconds, formatted_score, rank_title, extra_info)
VALUES 
    ('Mustafa_Ace', 'endless', 48, 0, 'DALGA 48', 'BAŞKOMUTAN AS', 'Elit As'),
    ('KaanLeader', 'endless', 45, 0, 'DALGA 45', 'BAŞKOMUTAN AS', 'Elit As'),
    ('GhostRider', 'endless', 41, 0, 'DALGA 41', 'HAVA ALBAYI', 'Elit As'),
    ('Viper_99', 'endless', 38, 0, 'DALGA 38', 'HAVA ALBAYI', 'Elit As'),
    ('SkyReaper', 'endless', 34, 0, 'DALGA 34', 'HAVA ALBAYI', 'Elit As')
ON CONFLICT (player_name, mode) DO NOTHING;
