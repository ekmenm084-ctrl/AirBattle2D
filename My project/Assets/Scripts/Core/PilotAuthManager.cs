using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Supabase PostgreSQL (RPC + bcrypt) + Çevrimdışı Yerel Yedekli Pilot Kimlik Doğrulama Yöneticisi.
/// - Her oyun açılışında IsAuthenticatedInSession = false olarak başlar (kullanıcı adı & şifre girişi zorunlu).
/// - Kayıt ve Giriş işlemlerini Supabase /rest/v1/rpc/register_pilot ve /rest/v1/rpc/login_pilot üzerinden yürütür.
/// - İnternet bağlantısı yoksa veya Supabase yapılandırılmamışsa cihaz içi tuzlanmış SHA-256 doğrulama ile yedekli çalışır.
/// </summary>
public class PilotAuthManager : MonoBehaviour
{
    public static PilotAuthManager Instance { get; private set; }

    /// <summary>
    /// Bu oyun oturumunda kullanıcı adı ve şifre ile doğrulama yapıldı mı?
    /// Her oyun açılışında false başlar; savaş sonrası ana menüye dönerken tekrar sormaz.
    /// </summary>
    public static bool IsAuthenticatedInSession { get; private set; } = false;

    public static string LastLoggedInUsername
    {
        get => PlayerPrefs.GetString("AirBattle_LastAuthUser", "");
        private set
        {
            PlayerPrefs.SetString("AirBattle_LastAuthUser", value ?? "");
            PlayerPrefs.Save();
        }
    }

    private const string LOCAL_HASH_PREFIX = "AirBattle_LocalAuthHash_";
    private const string LOCAL_SALT = "AB2D_TACTICAL_SALT_V1_";

    [Serializable]
    private class AuthRpcPayload
    {
        public string p_username;
        public string p_password;
    }

    [Serializable]
    private class AuthRpcResponse
    {
        public bool success;
        public string error;
        public string id;
        public string username;
        public int pilot_level;
        public int gold;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static PilotAuthManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var existing = FindAnyObjectByType<PilotAuthManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }
        GameObject go = new GameObject("PilotAuthManager");
        Instance = go.AddComponent<PilotAuthManager>();
        return Instance;
    }

    public void Logout()
    {
        IsAuthenticatedInSession = false;
    }

    public void Login(string username, string password, Action<bool, string> onComplete)
    {
        string cleanUser = ProfileManager.CleanProfileName(username);
        if (string.IsNullOrEmpty(username) || cleanUser.Length < 3)
        {
            onComplete?.Invoke(false, "Kullanıcı adı en az 3 karakter olmalıdır!");
            return;
        }
        if (string.IsNullOrEmpty(password) || password.Length < 4)
        {
            onComplete?.Invoke(false, "Şifre en az 4 karakter olmalıdır!");
            return;
        }

        StartCoroutine(AuthenticateCoroutine("login_pilot", cleanUser, password, isRegister: false, onComplete));
    }

    public void Register(string username, string password, Action<bool, string> onComplete)
    {
        string cleanUser = ProfileManager.CleanProfileName(username);
        if (string.IsNullOrEmpty(username) || cleanUser.Length < 3)
        {
            onComplete?.Invoke(false, "Kullanıcı adı en az 3 karakter olmalıdır!");
            return;
        }
        if (string.IsNullOrEmpty(password) || password.Length < 4)
        {
            onComplete?.Invoke(false, "Şifre en az 4 karakter olmalıdır!");
            return;
        }

        StartCoroutine(AuthenticateCoroutine("register_pilot", cleanUser, password, isRegister: true, onComplete));
    }

    private IEnumerator AuthenticateCoroutine(string rpcName, string cleanUser, string password, bool isRegister, Action<bool, string> onComplete)
    {
        var lb = LeaderboardManager.EnsureInstance();
        string baseUrl = lb != null ? lb.supabaseUrl : "";
        string anonKey = lb != null ? lb.supabaseAnonKey : "";

        bool hasSupabase = !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(anonKey) && baseUrl.StartsWith("http");

        if (!hasSupabase)
        {
            HandleLocalAuth(cleanUser, password, isRegister, onComplete);
            yield break;
        }

        string endpoint = baseUrl.TrimEnd('/') + "/rest/v1/rpc/" + rpcName;
        AuthRpcPayload payload = new AuthRpcPayload { p_username = cleanUser, p_password = password };
        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", anonKey);
            req.SetRequestHeader("Authorization", "Bearer " + anonKey);
            req.timeout = 10;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                // Çevrimdışıysa yerel önbellek doğrulamasına düş
                Debug.LogWarning("[PilotAuthManager] Sunucuya ulaşılamadı, çevrimdışı yerel doğrulama deneniyor.");
                HandleLocalAuth(cleanUser, password, isRegister, onComplete);
                yield break;
            }

            string responseText = req.downloadHandler != null ? req.downloadHandler.text : "";

            // Eğer RPC fonksiyonu henüz Supabase SQL Editor'de çalıştırılmamışsa (404) yerel yedekle devam et
            if (req.responseCode == 404)
            {
                Debug.LogWarning("[PilotAuthManager] Supabase RPC bulunamadı (supabase_schema.sql çalıştırılmalı). Yerel doğrulama kullanılıyor.");
                HandleLocalAuth(cleanUser, password, isRegister, onComplete);
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[PilotAuthManager] Sunucu yanıt hatası ({req.responseCode}): {responseText}");
                onComplete?.Invoke(false, "Sunucu doğrulama hatası! Lütfen tekrar deneyin.");
                yield break;
            }

            AuthRpcResponse res = null;
            try
            {
                res = JsonUtility.FromJson<AuthRpcResponse>(responseText);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PilotAuthManager] JSON ayrıştırma hatası: {ex.Message} | Yanıt: {responseText}");
            }

            if (res == null)
            {
                onComplete?.Invoke(false, "Beklenmeyen sunucu yanıtı.");
                yield break;
            }

            if (!res.success)
            {
                string errMsg = !string.IsNullOrEmpty(res.error) ? res.error : "Giriş başarısız.";
                onComplete?.Invoke(false, errMsg);
                yield break;
            }

            string finalUser = !string.IsNullOrEmpty(res.username) ? ProfileManager.CleanProfileName(res.username) : cleanUser;

            // Çevrimdışı yedek için yerel hash'i güncelle
            SaveLocalPasswordHash(finalUser, password);
            CompleteAuthenticatedLogin(finalUser);

            string okMsg = isRegister
                ? $"Hesap oluşturuldu! Hoş geldin, {finalUser}."
                : $"Giriş başarılı! Hoş geldin, {finalUser}.";
            onComplete?.Invoke(true, okMsg);
        }
    }

    private void HandleLocalAuth(string cleanUser, string password, bool isRegister, Action<bool, string> onComplete)
    {
        string key = LOCAL_HASH_PREFIX + cleanUser.ToLowerInvariant();
        string storedHash = PlayerPrefs.GetString(key, "");
        string inputHash = ComputeSha256(LOCAL_SALT + cleanUser.ToLowerInvariant() + ":" + password);

        if (isRegister)
        {
            if (!string.IsNullOrEmpty(storedHash))
            {
                onComplete?.Invoke(false, "Bu kullanıcı adı cihazda zaten kayıtlı!");
                return;
            }
            PlayerPrefs.SetString(key, inputHash);
            PlayerPrefs.Save();
            CompleteAuthenticatedLogin(cleanUser);
            onComplete?.Invoke(true, $"Hesap oluşturuldu! Hoş geldin, {cleanUser}.");
        }
        else
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                onComplete?.Invoke(false, "Kullanıcı bulunamadı! Önce Kayıt Ol sekmesinden hesap oluşturun.");
                return;
            }
            if (!string.Equals(storedHash, inputHash, StringComparison.Ordinal))
            {
                onComplete?.Invoke(false, "Hatalı şifre! Lütfen şifrenizi kontrol edin.");
                return;
            }
            CompleteAuthenticatedLogin(cleanUser);
            onComplete?.Invoke(true, $"Giriş başarılı! Hoş geldin, {cleanUser}.");
        }
    }

    private void SaveLocalPasswordHash(string cleanUser, string password)
    {
        string key = LOCAL_HASH_PREFIX + cleanUser.ToLowerInvariant();
        string hash = ComputeSha256(LOCAL_SALT + cleanUser.ToLowerInvariant() + ":" + password);
        PlayerPrefs.SetString(key, hash);
        PlayerPrefs.Save();
    }

    private void CompleteAuthenticatedLogin(string cleanUser)
    {
        IsAuthenticatedInSession = true;
        LastLoggedInUsername = cleanUser;
        ProfileManager.SwitchProfile(cleanUser);
        LeaderboardManager.EnsureInstance()?.SyncAllLocalRecordsToSupabase();
    }

    private static string ComputeSha256(string input)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
