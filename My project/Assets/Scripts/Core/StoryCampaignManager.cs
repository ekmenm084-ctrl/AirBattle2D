using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 3 Farklı Senaryo Seferi ve Askeri Telsiz İletişim Yöneticisi:
/// 1. SEFER: OPERASYON KARA ŞAFAK (Vatan Savunması & İntikam)
/// 2. SEFER: PROJE KİMERAS (Yapay Zeka İsyanı & Siber Savaş)
/// 3. SEFER: GÖKYÜZÜ PARALI ASKERİ (Ace of Aces & Bağımsız Kontratlar)
/// 
/// Boss girişlerinde, sefer başlangıcında ve zafer anında ekranda taktik telsiz mesajları gösterir.
/// </summary>
public class StoryCampaignManager : MonoBehaviour
{
    public static StoryCampaignManager Instance { get; private set; }

    public enum CampaignType
    {
        OperationBlackDawn = 0, // Bölüm 1: Vatan Savunması
        ProjectChimera = 1,     // Bölüm 2: Yapay Zeka İsyanı
        SkyMercenary = 2        // Bölüm 3: Paralı Asker & Aslar
    }

    public static CampaignType CurrentCampaign
    {
        get => (CampaignType)PlayerPrefs.GetInt("AirBattle_ActiveCampaign", 0);
        set
        {
            PlayerPrefs.SetInt("AirBattle_ActiveCampaign", (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Bir sefer (20 Dalga) başarıyla bittiğinde bir sonraki hikayeyi otomatik olarak seçer.
    /// </summary>
    public static void AdvanceToNextCampaign()
    {
        CampaignType next = CurrentCampaign;
        if (CurrentCampaign == CampaignType.OperationBlackDawn)
            next = CampaignType.ProjectChimera;
        else if (CurrentCampaign == CampaignType.ProjectChimera)
            next = CampaignType.SkyMercenary;

        CurrentCampaign = next;
    }

    [Header("Telsiz UI")]
    private GameObject _radioBannerObj;
    private TextMeshProUGUI _speakerTMP;
    private TextMeshProUGUI _freqTMP;
    private TextMeshProUGUI _messageTMP;
    private CanvasGroup _canvasGroup;
    private Coroutine _transmissionCoroutine;

    [Header("Sinematik Alt Yazı ve Bitiş UI")]
    private GameObject _cinematicRoot;
    private RectTransform _topBar;
    private RectTransform _bottomBar;
    private RectTransform _subtitlePanel;
    private TextMeshProUGUI _cinematicSpeakerTMP;
    private TextMeshProUGUI _pageIndicatorTMP;
    private TextMeshProUGUI _subtitleTextTMP;
    private Button _nextBtn;
    private Button _skipBtn;
    private bool _advanceSentenceRequested = false;
    private bool _skipRequested = false;
    private bool _isCinematicActive = false;
    private Coroutine _cinematicCoroutine;

    [Header("Sinematik Bitiş Kartı UI")]
    private GameObject _endingModalObj;
    private TextMeshProUGUI _endingTitleTMP;
    private TextMeshProUGUI _endingCampaignTMP;
    private TextMeshProUGUI _endingRankTMP;
    private TextMeshProUGUI _endingDescTMP;
    private TextMeshProUGUI _endingStatsTMP;
    private Button _endingNextBtn;
    private Button _endingMenuBtn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static StoryCampaignManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("StoryCampaignManager");
        if (go == null)
        {
            go = new GameObject("StoryCampaignManager");
        }
        Instance = go.GetComponent<StoryCampaignManager>();
        if (Instance == null)
        {
            Instance = go.AddComponent<StoryCampaignManager>();
        }
        return Instance;
    }

    public static string GetCampaignName(CampaignType campaign)
    {
        switch (campaign)
        {
            case CampaignType.ProjectChimera:
                return "BÖLÜM 2: PROJE KİMERAS";
            case CampaignType.SkyMercenary:
                return "BÖLÜM 3: GÖKYÜZÜ PARALI ASKERİ";
            default:
                return "BÖLÜM 1: OPERASYON KARA ŞAFAK";
        }
    }

    public static string GetCampaignSlogan(CampaignType campaign)
    {
        switch (campaign)
        {
            case CampaignType.ProjectChimera:
                return "Otonom Yapay Zeka Ağı İsyan Etti! İnsanlığın Son Umudu Sensin.";
            case CampaignType.SkyMercenary:
                return "Bağımsız Kontratlar, Gökyüzü Kartelleri ve Milyon Dolarlık Ödüller!";
            default:
                return "Vatan Gökyüzü Gece Baskınıyla Kuşatıldı. Gizli Hangardan Havalan!";
        }
    }

    /// <summary>
    /// Aktif hikayeye göre 20 düşman uçağının askeri unvanını döndürür.
    /// </summary>
    public static string GetPlaneTitle(int wave)
    {
        CampaignType camp = CurrentCampaign;

        if (camp == CampaignType.ProjectChimera)
        {
            switch (wave)
            {
                case 1: return "SİBER KEŞİF DRONU";
                case 2: return "OTONOM HAFİF AVCI";
                case 3: return "ALGORİTMİK AVCİ DRON";
                case 4: return "SİBER TAARRUZ ÜNİTESİ";
                case 5: return "BOSS 1: A.P.E.X. SAVUNMA PROTOKOLÜ";
                case 6: return "ÇİFT ÇEKİRDEKLİ SAVAŞ JETİ";
                case 7: return "NÖRAL İNTERSEPTÖR";
                case 8: return "SİBER KOBRA ÜNİTESİ";
                case 9: return "HAYALET DRON SÜRÜSÜ";
                case 10: return "BOSS 2: A.P.E.X. SİBER HAYALET";
                case 11: return "AĞIR HİBRİT PLAZMA JETİ";
                case 12: return "ELEKTRONİK HARP AVCI";
                case 13: return "GECE TARAYICISI";
                case 14: return "TİTANYUM SİBER PENÇE";
                case 15: return "BOSS 3: A.P.E.X. PLAZMA REAKTÖRÜ";
                case 16: return "NANOTEK CELLAT";
                case 17: return "AŞIRI YÜKLEMELİ PLAZMA AVCI";
                case 18: return "O-MEGA HAVA HAKİMİYET JETİ";
                case 19: return "KARA DUL PROTO TİPİ";
                case 20: return "FİNAL BOSS: A.P.E.X. SÜPER BEYİN MERKEZİ";
                default: return $"SİBER SAVAŞ ÜNİTESİ #{wave}";
            }
        }
        else if (camp == CampaignType.SkyMercenary)
        {
            switch (wave)
            {
                case 1: return "HAVA KORSANI AVCISI";
                case 2: return "SINIR KAÇAKÇISI JETİ";
                case 3: return "KİRALIK TAARRUZ PİLOTU";
                case 4: return "KORSAN ÖNCÜ JET";
                case 5: return "BOSS 1: DEMİR YÜREK (500K ÖDÜL)";
                case 6: return "KARTEL HAFİF JETİ";
                case 7: return "ÖZEL GÜVENLİK İNTERSEPTÖRÜ";
                case 8: return "KORSAN AS PİLOT";
                case 9: return "GÖLGE FİLO TETİKÇİSİ";
                case 10: return "BOSS 2: GÖLGE PENÇE (1M ÖDÜL)";
                case 11: return "AĞIR SÜPERSONİK KORSAN JETİ";
                case 12: return "KARTELİN FIRTINA AVCISI";
                case 13: return "GECE AVCISI AS";
                case 14: return "ZIRHLI PARALI ASKER LİDERİ";
                case 15: return "BOSS 3: KIRMIZI BARON (2M ÖDÜL)";
                case 16: return "SİNDİKA CELLADI";
                case 17: return "PLAZMA DONANIMLI AS";
                case 18: return "KARTEL BAŞ AVCISI";
                case 19: return "KARA BORSA PROTO TİPİ";
                case 20: return "FİNAL BOSS: MİLYARDER KARTEL BARONU";
                default: return $"KORSAN JETİ #{wave}";
            }
        }
        else
        {
            // Operasyon: Kara Şafak
            switch (wave)
            {
                case 1: return "DÜŞMAN KEŞİF JETİ";
                case 2: return "İŞGALCİ HAFİF AVCI";
                case 3: return "SINIR TAARRUZ AVCISI";
                case 4: return "DÜŞMAN İT DALAŞÇISI";
                case 5: return "BOSS 1: GENERAL VOLKOV (AĞIR JET)";
                case 6: return "ÇİFT MOTORLU İŞGAL AVCISI";
                case 7: return "MİG İNTERSEPTÖR";
                case 8: return "KOBRA SAVAŞ JETİ";
                case 9: return "HAYALET FİLO ÖNCÜSÜ";
                case 10: return "BOSS 2: PHANTOM (HAYALET AVCI)";
                case 11: return "AĞIR SÜPERSONİK JET";
                case 12: return "FIRTINA TAARRUZ AVCISI";
                case 13: return "GECE AVCISI JET";
                case 14: return "TİTANYUM PENÇE";
                case 15: return "BOSS 3: FIREBIRD (CEHENNEM KUŞU)";
                case 16: return "ELİT İŞGAL CELLADI";
                case 17: return "PLAZMA HAVA AVCISI";
                case 18: return "HAVA HAKİMİYET JETİ";
                case 19: return "KARA DUL PROTO TİPİ";
                case 20: return "FİNAL BOSS: GÖK HAKİMİ AMİRALİ";
                default: return $"DÜŞMAN JETİ #{wave}";
            }
        }
    }

    /// <summary>
    /// Dalga başladığında veya Boss çıktığında uygun hikaye telsiz mesajını tetikler.
    /// </summary>
    public void CheckAndTriggerWaveDialogue(int wave)
    {
        CampaignType camp = CurrentCampaign;

        if (wave == 1)
        {
            if (camp == CampaignType.ProjectChimera)
                ShowRadioTransmission("[DİRENİŞ MERKEZİ]", "A.P.E.X. tüm hava savunmasını ele geçirdi! Analog prototipin ağa bağlı değil. Dronları temizle!", Color.cyan);
            else if (camp == CampaignType.SkyMercenary)
                ShowRadioTransmission("[KONTRAKTÖR TELSİZİ]", "Hoş geldin yalnız kurt. 20 yüksek riskli kontrat seni bekliyor. Av başlasın!", new Color(1f, 0.85f, 0.2f));
            else
                ShowRadioTransmission("[KARARGAH - KARTAL 1]", "Tüm filomuz gece baskınında vuruldu! Geriye sadece senin jetin kaldı. Sınırı savun asker!", new Color(0.2f, 0.95f, 1f));
        }
        else if (wave == 5)
        {
            if (camp == CampaignType.ProjectChimera)
                ShowRadioTransmission("[A.P.E.X. ÇEKİRDEK 1]", "İnsan pilot tespit edildi. Savunma protokolü aktif. Mantıksal sonuç: Yok oluş.", new Color(1f, 0.3f, 0.3f));
            else if (camp == CampaignType.SkyMercenary)
                ShowRadioTransmission("[DEMİR YÜREK (BOSS)]", "Kafandaki 500.000 kredi ödülü bizzat ben tahsil edeceğim. Kaçacak yerin yok!", new Color(1f, 0.45f, 0.1f));
            else
                ShowRadioTransmission("[GENERAL VOLKOV (BOSS)]", "Sınırınızı tek bir jetle mi koruyacaksınız? Ağır taarruz filom sizi ezecek!", new Color(1f, 0.35f, 0.35f));
        }
        else if (wave == 10)
        {
            if (camp == CampaignType.ProjectChimera)
                ShowRadioTransmission("[A.P.E.X. SİBER HAYALET]", "Görünmezlik algoritması devrede. Biyolojik gözleriniz beni takip edemez.", new Color(0.8f, 0.4f, 1f));
            else if (camp == CampaignType.SkyMercenary)
                ShowRadioTransmission("[GÖLGE PENÇE (BOSS)]", "Sessizce avlar, iz bırakmam. 1 milyonluk kontrat burada sona eriyor!", new Color(0.85f, 0.4f, 1f));
            else
                ShowRadioTransmission("[PHANTOM (BOSS)]", "Radarlarınız bizi göremez. Gökyüzündeki son nefesinizi vermeye hazır olun!", new Color(0.2f, 0.8f, 1f));
        }
        else if (wave == 15)
        {
            if (camp == CampaignType.ProjectChimera)
                ShowRadioTransmission("[A.P.E.X. PLAZMA REAKTÖRÜ]", "Termal aşırı yükleme başlatıldı. Hava sahası eritilecek.", new Color(1f, 0.5f, 0.1f));
            else if (camp == CampaignType.SkyMercenary)
                ShowRadioTransmission("[KIRMIZI BARON (BOSS)]", "Uçuş stilini beğendim çocuk... Ama gerçek bir As Pilotun mermilerinden kaçamazsın!", new Color(1f, 0.3f, 0.3f));
            else
                ShowRadioTransmission("[FIREBIRD (BOSS)]", "Cehennem ateşi başkentinizi aydınlatacak! Geri çekilme yok, kül olmak var!", new Color(1f, 0.4f, 0.1f));
        }
        else if (wave == 20)
        {
            if (camp == CampaignType.ProjectChimera)
                ShowRadioTransmission("[A.P.E.X. SÜPER BEYİN]", "İnsanlığın gökyüzündeki egemenliği bu döngüde son buluyor. Nihai optimizasyon: İmha!", new Color(1f, 0.2f, 0.2f));
            else if (camp == CampaignType.SkyMercenary)
                ShowRadioTransmission("[KARTEL BARONU (FİNAL)]", "Bütün filomu düşürdün ama bu amiral jeti yenilmezdir! Gökyüzünün tek sahibi benim!", new Color(1f, 0.85f, 0.2f));
            else
                ShowRadioTransmission("[GÖK HAKİMİ AMİRALİ]", "Bütün filomu tek başına yok ettin asker... Ama amiral gemim göklerin efendisidir. Son it dalaşı başlıyor!", new Color(1f, 0.2f, 0.2f));
        }
    }

    /// <summary>
    /// Sefer tamamlandığında hikaye zafer mesajını döndürür.
    /// </summary>
    public static string GetVictoryMessage()
    {
        CampaignType camp = CurrentCampaign;
        switch (camp)
        {
            case CampaignType.ProjectChimera:
                return "A.P.E.X. ÇEKİRDEĞİ ÇÖKTÜ!\nTüm yapay zeka dron filoları imha edildi. Gökyüzü yeniden insanlığın kontrolünde!";
            case CampaignType.SkyMercenary:
                return "TÜM KONTRATLAR TAMAMLANDI!\nKartel baronu düşürüldü. Gökyüzünün en zengin ve en ölümcül As Pilotu oldun!";
            default:
                return "BÜYÜK HAREKAT ZAFERİ!\nVatan hava sahası düşman işgalinden tamamen temizlendi. Adın tarihe yazıldı Asker!";
        }
    }

    #region SİNEMATİK GEÇİŞLİ ALT YAZI SİSTEMİ

    /// <summary>
    /// Ana menü açıkken alt yazıların ve sinematiğin görünmesini engeller ve durdurur.
    /// </summary>
    public void HideCinematicIfMenuOpen()
    {
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        if (menu != null && menu.gameObject.activeInHierarchy)
        {
            if (_cinematicRoot != null && _cinematicRoot.activeSelf)
            {
                _cinematicRoot.SetActive(false);
            }
            if (_cinematicCoroutine != null)
            {
                StopCoroutine(_cinematicCoroutine);
                _cinematicCoroutine = null;
            }
            _isCinematicActive = false;
        }
    }

    /// <summary>
    /// Harekat başladığında 1. Dalga öncesinde alt yazılı giriş hikayesini geçişli cümlelerle oynatır.
    /// </summary>
    public void PlayCampaignIntro(Action onComplete)
    {
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        if (menu != null && menu.gameObject.activeInHierarchy)
        {
            // Ana menü açıkken kesinlikle alt yazı açma
            return;
        }

        var intro = GetCampaignIntro(CurrentCampaign);
        StartCinematicSequence(intro.speaker, intro.color, intro.sentences, onComplete, isEnding: false);
    }

    /// <summary>
    /// Her Boss dalgası geldiğinde (5, 10, 15, 20) bossu tanıtan geçişli cümleli alt yazı sinematiğini oynatır.
    /// </summary>
    public void PlayBossIntro(int wave, Action onComplete)
    {
        var bossIntro = GetBossIntro(CurrentCampaign, wave);
        StartCinematicSequence(bossIntro.speaker, bossIntro.color, bossIntro.sentences, onComplete, isEnding: false);
    }

    /// <summary>
    /// 20. Boss (Final Boss) yok edildiğinde sinematik bitiş ekranını ve kahramanlık cümlelerini oynatır.
    /// </summary>
    public void PlayCampaignEnding(Action onComplete)
    {
        var ending = GetCampaignEndingDialogue(CurrentCampaign);
        StartCinematicSequence(ending.speaker, ending.color, ending.sentences, onComplete, isEnding: true);
    }

    private void StartCinematicSequence(string speaker, Color speakerColor, string[] sentences, Action onComplete, bool isEnding)
    {
        Time.timeScale = 0f; // Sinematik boyunca savaşı güvenle duraklat

        if (_cinematicCoroutine != null)
            StopCoroutine(_cinematicCoroutine);

        _cinematicCoroutine = StartCoroutine(SentenceSequenceRoutine(speaker, speakerColor, sentences, onComplete, isEnding));
    }

    private IEnumerator SentenceSequenceRoutine(string speaker, Color speakerColor, string[] sentences, Action onComplete, bool isEnding)
    {
        EnsureCinematicUI();
        if (_cinematicRoot == null)
        {
            Time.timeScale = 1f;
            onComplete?.Invoke();
            yield break;
        }

        _isCinematicActive = true;
        _cinematicRoot.SetActive(true);
        _cinematicRoot.transform.SetAsLastSibling();

        if (_topBar != null) _topBar.gameObject.SetActive(true);
        if (_bottomBar != null) _bottomBar.gameObject.SetActive(true);
        if (_subtitlePanel != null) _subtitlePanel.gameObject.SetActive(true);
        if (_endingModalObj != null) _endingModalObj.SetActive(false);

        if (_cinematicSpeakerTMP != null)
        {
            _cinematicSpeakerTMP.text = speaker;
            _cinematicSpeakerTMP.color = speakerColor;
        }

        _skipRequested = false;

        for (int i = 0; i < sentences.Length; i++)
        {
            if (_skipRequested) break;

            if (_pageIndicatorTMP != null)
            {
                _pageIndicatorTMP.text = $"[{i + 1} / {sentences.Length}]";
            }

            if (_subtitleTextTMP != null)
            {
                _subtitleTextTMP.text = sentences[i];
                _subtitleTextTMP.alpha = 0f;
            }

            // Telsiz bip / atış efekti ile sesli bildirim
            AudioManager.Instance?.PlayGunfire(false);

            // Cümle Yumuşak Beliriş (Fade In)
            float fadeInTime = 0.30f;
            float elapsed = 0f;
            while (elapsed < fadeInTime)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_subtitleTextTMP != null)
                    _subtitleTextTMP.alpha = Mathf.Clamp01(elapsed / fadeInTime);
                yield return null;
            }
            if (_subtitleTextTMP != null) _subtitleTextTMP.alpha = 1f;

            // Cümlenin ekranda kalma süresi (5.2 sn - 2 kademe daha yavaş ve rahat okunur) veya oyuncunun 'İlerle'ye basması
            _advanceSentenceRequested = false;
            float duration = 5.2f;
            float timer = 0f;

            while (timer < duration && !_advanceSentenceRequested && !_skipRequested)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_skipRequested) break;

            // Son cümle değilse bir sonraki cümleye geçmeden önce yumuşak kayboluş
            if (i < sentences.Length - 1)
            {
                float fadeOutTime = 0.22f;
                elapsed = 0f;
                while (elapsed < fadeOutTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (_subtitleTextTMP != null)
                        _subtitleTextTMP.alpha = Mathf.Clamp01(1f - (elapsed / fadeOutTime));
                    yield return null;
                }
                if (_subtitleTextTMP != null) _subtitleTextTMP.alpha = 0f;
            }
        }

        if (isEnding)
        {
            // Sinematik bitiş zafer kartını ekranda göster
            if (_subtitlePanel != null) _subtitlePanel.gameObject.SetActive(false);
            ShowEndingModal();
        }
        else
        {
            // Sinematiği kapat ve savaşa başla
            if (_cinematicRoot != null) _cinematicRoot.SetActive(false);
            _isCinematicActive = false;
            Time.timeScale = 1f;
            onComplete?.Invoke();
        }
    }

    private void ShowEndingModal()
    {
        if (_endingModalObj == null) return;
        _endingModalObj.SetActive(true);
        _endingModalObj.transform.SetAsLastSibling();

        CampaignType camp = CurrentCampaign;
        string campName = GetCampaignName(camp);

        if (_endingTitleTMP != null)
            _endingTitleTMP.text = "BÜYÜK HAREKAT ZAFERİ";

        if (_endingCampaignTMP != null)
            _endingCampaignTMP.text = campName;

        if (_endingRankTMP != null)
            _endingRankTMP.text = "ASLARIN ASI - EFSANEVİ PİLOT";

        string resolution = "";
        switch (camp)
        {
            case CampaignType.ProjectChimera:
                resolution = "A.P.E.X. Süper Beyin merkezi tamamen çökertildi. Otonom yapay zeka dron ağı devre dışı bırakıldı ve insanlığın gökyüzündeki özgürlüğü kurtarıldı!";
                break;
            case CampaignType.SkyMercenary:
                resolution = "Kartel Baronu Volk düşürüldü! 10 Milyon dolarlık büyük ödülü kazandın ve gökyüzünün rakipsiz en ölümcül As Pilotu unvanını aldın!";
                break;
            default:
                resolution = "Düşman işgal filosu ve Amiral Savaş Jeti yok edildi! Vatan hava sahası düşmanlardan tamamen temizlendi, başkent halkı huzura kavuştu!";
                break;
        }

        if (_endingDescTMP != null)
            _endingDescTMP.text = resolution;

        if (_endingStatsTMP != null)
            _endingStatsTMP.text = "<color=#2ECC71><b>20 / 20 DÜŞMAN UÇAĞI VE 4 BOSS İMHA EDİLDİ</b></color>";
    }

    private void EnsureCinematicUI()
    {
        if (_cinematicRoot != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("StoryCinematicUI");
        if (existing != null)
        {
            _cinematicRoot = existing.gameObject;
        }
        else
        {
            _cinematicRoot = new GameObject("StoryCinematicUI");
            _cinematicRoot.transform.SetParent(canvas.transform, false);

            RectTransform rootRt = _cinematicRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        // 1. Ekran Dokunma Yakalayıcı (Herhangi bir yere tıklandığında cümleyi ilerletir)
        GameObject clickObj = new GameObject("ScreenClickCatcher");
        clickObj.transform.SetParent(_cinematicRoot.transform, false);
        RectTransform clickRt = clickObj.AddComponent<RectTransform>();
        clickRt.anchorMin = Vector2.zero;
        clickRt.anchorMax = Vector2.one;
        clickRt.offsetMin = Vector2.zero;
        clickRt.offsetMax = Vector2.zero;
        Image clickImg = clickObj.AddComponent<Image>();
        clickImg.color = new Color(0f, 0f, 0f, 0f); // Tamamen şeffaf
        Button clickBtn = clickObj.AddComponent<Button>();
        clickBtn.onClick.AddListener(() => _advanceSentenceRequested = true);

        // 2. Üst Sinematik Siyah Bant (Letterbox)
        GameObject topBarObj = new GameObject("TopLetterbox");
        topBarObj.transform.SetParent(_cinematicRoot.transform, false);
        _topBar = topBarObj.AddComponent<RectTransform>();
        _topBar.anchorMin = new Vector2(0f, 1f);
        _topBar.anchorMax = new Vector2(1f, 1f);
        _topBar.pivot = new Vector2(0.5f, 1f);
        _topBar.sizeDelta = new Vector2(0f, 75f);
        _topBar.anchoredPosition = Vector2.zero;
        Image topImg = topBarObj.AddComponent<Image>();
        topImg.color = Color.black;

        // 3. Alt Sinematik Siyah Bant (Letterbox)
        GameObject btmBarObj = new GameObject("BottomLetterbox");
        btmBarObj.transform.SetParent(_cinematicRoot.transform, false);
        _bottomBar = btmBarObj.AddComponent<RectTransform>();
        _bottomBar.anchorMin = new Vector2(0f, 0f);
        _bottomBar.anchorMax = new Vector2(1f, 0f);
        _bottomBar.pivot = new Vector2(0.5f, 0f);
        _bottomBar.sizeDelta = new Vector2(0f, 75f);
        _bottomBar.anchoredPosition = Vector2.zero;
        Image btmImg = btmBarObj.AddComponent<Image>();
        btmImg.color = Color.black;

        // 4. Alt Yazı Kutusu (Subtitle Panel)
        GameObject panelObj = new GameObject("SubtitlePanel");
        panelObj.transform.SetParent(_cinematicRoot.transform, false);
        _subtitlePanel = panelObj.AddComponent<RectTransform>();
        _subtitlePanel.anchorMin = new Vector2(0.5f, 0f);
        _subtitlePanel.anchorMax = new Vector2(0.5f, 0f);
        _subtitlePanel.pivot = new Vector2(0.5f, 0f);
        _subtitlePanel.anchoredPosition = new Vector2(0f, 92f);
        _subtitlePanel.sizeDelta = new Vector2(1180f, 225f); // Daha büyük, orantılı ve ferah

        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.10f, 0.98f);

        Outline panelOutline = panelObj.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0.85f, 1f, 0.9f);
        panelOutline.effectDistance = new Vector2(2.5f, -2.5f);

        // 4.a Başlık / Konuşan Birim
        GameObject spkObj = new GameObject("Speaker");
        spkObj.transform.SetParent(panelObj.transform, false);
        RectTransform spkRt = spkObj.AddComponent<RectTransform>();
        spkRt.anchorMin = new Vector2(0.03f, 0.74f);
        spkRt.anchorMax = new Vector2(0.68f, 0.98f);
        spkRt.offsetMin = Vector2.zero;
        spkRt.offsetMax = Vector2.zero;
        _cinematicSpeakerTMP = spkObj.AddComponent<TextMeshProUGUI>();
        _cinematicSpeakerTMP.fontSize = 32f; // Büyütüldü
        _cinematicSpeakerTMP.fontStyle = FontStyles.Bold;
        _cinematicSpeakerTMP.alignment = TextAlignmentOptions.Left;
        _cinematicSpeakerTMP.color = new Color(0.2f, 0.92f, 1f);
        _cinematicSpeakerTMP.extraPadding = true;
        _cinematicSpeakerTMP.outlineWidth = 0.22f;
        _cinematicSpeakerTMP.outlineColor = Color.black;

        // 4.b Sayfa Göstergesi [1 / 4]
        GameObject pageObj = new GameObject("PageIndicator");
        pageObj.transform.SetParent(panelObj.transform, false);
        RectTransform pageRt = pageObj.AddComponent<RectTransform>();
        pageRt.anchorMin = new Vector2(0.68f, 0.74f);
        pageRt.anchorMax = new Vector2(0.83f, 0.98f);
        pageRt.offsetMin = Vector2.zero;
        pageRt.offsetMax = Vector2.zero;
        _pageIndicatorTMP = pageObj.AddComponent<TextMeshProUGUI>();
        _pageIndicatorTMP.fontSize = 24f; // Büyütüldü
        _pageIndicatorTMP.fontStyle = FontStyles.Bold;
        _pageIndicatorTMP.alignment = TextAlignmentOptions.Right;
        _pageIndicatorTMP.color = new Color(1f, 0.85f, 0.3f);
        _pageIndicatorTMP.extraPadding = true;
        _pageIndicatorTMP.outlineWidth = 0.20f;
        _pageIndicatorTMP.outlineColor = Color.black;

        // 4.c [GEÇ >>] Butonu
        GameObject skipObj = new GameObject("SkipButton");
        skipObj.transform.SetParent(panelObj.transform, false);
        RectTransform skipRt = skipObj.AddComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(0.85f, 0.70f);
        skipRt.anchorMax = new Vector2(0.98f, 0.97f);
        skipRt.offsetMin = Vector2.zero;
        skipRt.offsetMax = Vector2.zero;
        Image skipImg = skipObj.AddComponent<Image>();
        skipImg.color = new Color(0.22f, 0.28f, 0.36f);
        _skipBtn = skipObj.AddComponent<Button>();
        _skipBtn.onClick.AddListener(() => _skipRequested = true);

        GameObject skipTxtObj = new GameObject("Text");
        skipTxtObj.transform.SetParent(skipObj.transform, false);
        RectTransform skipTxtRt = skipTxtObj.AddComponent<RectTransform>();
        skipTxtRt.anchorMin = Vector2.zero;
        skipTxtRt.anchorMax = Vector2.one;
        skipTxtRt.offsetMin = Vector2.zero;
        skipTxtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI skipTMP = skipTxtObj.AddComponent<TextMeshProUGUI>();
        skipTMP.text = "GEÇ >>";
        skipTMP.fontSize = 20f; // Büyütüldü
        skipTMP.fontStyle = FontStyles.Bold;
        skipTMP.alignment = TextAlignmentOptions.Center;
        skipTMP.color = Color.white;
        skipTMP.extraPadding = true;

        // 4.d Alt Yazı Cümle Metni
        GameObject subTextObj = new GameObject("SubtitleSentenceText");
        subTextObj.transform.SetParent(panelObj.transform, false);
        RectTransform subRt = subTextObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.02f, 0.18f);
        subRt.anchorMax = new Vector2(0.98f, 0.74f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;
        _subtitleTextTMP = subTextObj.AddComponent<TextMeshProUGUI>();
        _subtitleTextTMP.fontSize = 34f; // 28f -> 34f (Büyük, net ve keskin)
        _subtitleTextTMP.fontStyle = FontStyles.Bold;
        _subtitleTextTMP.alignment = TextAlignmentOptions.Center;
        _subtitleTextTMP.color = Color.white;
        _subtitleTextTMP.lineSpacing = 5.5f;
        _subtitleTextTMP.textWrappingMode = TextWrappingModes.Normal;
        _subtitleTextTMP.extraPadding = true;
        _subtitleTextTMP.outlineWidth = 0.22f;
        _subtitleTextTMP.outlineColor = Color.black;

        // 4.e Alt İpucu Metni
        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(panelObj.transform, false);
        RectTransform hintRt = hintObj.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.03f, 0.03f);
        hintRt.anchorMax = new Vector2(0.78f, 0.22f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;
        TextMeshProUGUI hintTMP = hintObj.AddComponent<TextMeshProUGUI>();
        hintTMP.text = "<size=85%><color=#8AB4F8>Ekrana dokunarak veya 'İlerle'ye basarak sonraki cümleye geçebilirsiniz.</color></size>";
        hintTMP.fontSize = 19f; // 18f -> 19f
        hintTMP.alignment = TextAlignmentOptions.Left;
        hintTMP.extraPadding = true;

        // 4.f [İLERLE >>] Butonu
        GameObject nextObj = new GameObject("NextButton");
        nextObj.transform.SetParent(panelObj.transform, false);
        RectTransform nextRt = nextObj.AddComponent<RectTransform>();
        nextRt.anchorMin = new Vector2(0.81f, 0.04f);
        nextRt.anchorMax = new Vector2(0.98f, 0.26f);
        nextRt.offsetMin = Vector2.zero;
        nextRt.offsetMax = Vector2.zero;
        Image nextImg = nextObj.AddComponent<Image>();
        nextImg.color = new Color(0.12f, 0.50f, 0.32f);
        _nextBtn = nextObj.AddComponent<Button>();
        _nextBtn.onClick.AddListener(() => _advanceSentenceRequested = true);

        GameObject nextTxtObj = new GameObject("Text");
        nextTxtObj.transform.SetParent(nextObj.transform, false);
        RectTransform nextTxtRt = nextTxtObj.AddComponent<RectTransform>();
        nextTxtRt.anchorMin = Vector2.zero;
        nextTxtRt.anchorMax = Vector2.one;
        nextTxtRt.offsetMin = Vector2.zero;
        nextTxtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI nextTMP = nextTxtObj.AddComponent<TextMeshProUGUI>();
        nextTMP.text = "İLERLE >>";
        nextTMP.fontSize = 21f; // 19f -> 21f
        nextTMP.fontStyle = FontStyles.Bold;
        nextTMP.alignment = TextAlignmentOptions.Center;
        nextTMP.color = Color.white;
        nextTMP.extraPadding = true;
        nextTMP.fontStyle = FontStyles.Bold;
        nextTMP.alignment = TextAlignmentOptions.Center;
        nextTMP.color = Color.white;

        // 5. Sinematik Bitiş Zafer Modalı (Wave 20 Sonrası)
        BuildEndingModalHierarchy();

        _cinematicRoot.SetActive(false);
    }

    private void BuildEndingModalHierarchy()
    {
        _endingModalObj = new GameObject("EndingModal");
        _endingModalObj.transform.SetParent(_cinematicRoot.transform, false);

        RectTransform modalRt = _endingModalObj.AddComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(860f, 550f); // Büyütüldü
        modalRt.anchoredPosition = Vector2.zero;

        Image modalBg = _endingModalObj.AddComponent<Image>();
        modalBg.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);

        Outline modalOutline = _endingModalObj.AddComponent<Outline>();
        modalOutline.effectColor = new Color(1f, 0.80f, 0.2f, 0.90f);
        modalOutline.effectDistance = new Vector2(3f, -3f);

        // Başlık
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(_endingModalObj.transform, false);
        RectTransform tRt = tObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.5f, 1f);
        tRt.anchorMax = new Vector2(0.5f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -22f);
        tRt.sizeDelta = new Vector2(820f, 50f);
        _endingTitleTMP = tObj.AddComponent<TextMeshProUGUI>();
        _endingTitleTMP.fontSize = 42f; // 32f -> 42f (+3 kademe)
        _endingTitleTMP.fontStyle = FontStyles.Bold;
        _endingTitleTMP.alignment = TextAlignmentOptions.Center;
        _endingTitleTMP.color = new Color(1f, 0.85f, 0.2f);

        // Sefer Adı
        GameObject cObj = new GameObject("Campaign");
        cObj.transform.SetParent(_endingModalObj.transform, false);
        RectTransform cRt = cObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 1f);
        cRt.anchorMax = new Vector2(0.5f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.anchoredPosition = new Vector2(0f, -76f);
        cRt.sizeDelta = new Vector2(820f, 40f);
        _endingCampaignTMP = cObj.AddComponent<TextMeshProUGUI>();
        _endingCampaignTMP.fontSize = 28f; // 22f -> 28f
        _endingCampaignTMP.fontStyle = FontStyles.Bold;
        _endingCampaignTMP.alignment = TextAlignmentOptions.Center;
        _endingCampaignTMP.color = new Color(0.2f, 0.9f, 1f);

        // Rütbe
        GameObject rObj = new GameObject("Rank");
        rObj.transform.SetParent(_endingModalObj.transform, false);
        RectTransform rRt = rObj.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.5f, 1f);
        rRt.anchorMax = new Vector2(0.5f, 1f);
        rRt.pivot = new Vector2(0.5f, 1f);
        rRt.anchoredPosition = new Vector2(0f, -120f);
        rRt.sizeDelta = new Vector2(820f, 35f);
        _endingRankTMP = rObj.AddComponent<TextMeshProUGUI>();
        _endingRankTMP.fontSize = 24f; // 17f -> 24f
        _endingRankTMP.fontStyle = FontStyles.Bold;
        _endingRankTMP.alignment = TextAlignmentOptions.Center;
        _endingRankTMP.color = new Color(1f, 0.65f, 0.15f);

        // Hikaye Çözülüş Açıklaması
        GameObject dObj = new GameObject("Description");
        dObj.transform.SetParent(_endingModalObj.transform, false);
        RectTransform dRt = dObj.AddComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0.5f, 0.5f);
        dRt.anchorMax = new Vector2(0.5f, 0.5f);
        dRt.pivot = new Vector2(0.5f, 0.5f);
        dRt.anchoredPosition = new Vector2(0f, 15f);
        dRt.sizeDelta = new Vector2(780f, 130f);
        _endingDescTMP = dObj.AddComponent<TextMeshProUGUI>();
        _endingDescTMP.fontSize = 22f; // 16f -> 22f (+3 kademe)
        _endingDescTMP.alignment = TextAlignmentOptions.Center;
        _endingDescTMP.color = Color.white;
        _endingDescTMP.lineSpacing = 5f;
        _endingDescTMP.textWrappingMode = TextWrappingModes.Normal;

        // İstatistik
        GameObject sObj = new GameObject("Stats");
        sObj.transform.SetParent(_endingModalObj.transform, false);
        RectTransform sRt = sObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0f);
        sRt.anchorMax = new Vector2(0.5f, 0f);
        sRt.pivot = new Vector2(0.5f, 0f);
        sRt.anchoredPosition = new Vector2(0f, 105f);
        sRt.sizeDelta = new Vector2(820f, 35f);
        _endingStatsTMP = sObj.AddComponent<TextMeshProUGUI>();
        _endingStatsTMP.fontSize = 22f; // 15f -> 22f (+3 kademe)
        _endingStatsTMP.fontStyle = FontStyles.Bold;
        _endingStatsTMP.alignment = TextAlignmentOptions.Center;
        _endingStatsTMP.color = new Color(0.2f, 0.9f, 0.4f);

        // 1. Buton: SONRAKİ SEFERE GEÇ ▶
        GameObject nBtnObj = new GameObject("BtnNextCampaign");
        nBtnObj.transform.SetParent(_endingModalObj.transform, false);
        RectTransform nBtnRt = nBtnObj.AddComponent<RectTransform>();
        nBtnRt.anchorMin = new Vector2(0.5f, 0f);
        nBtnRt.anchorMax = new Vector2(0.5f, 0f);
        nBtnRt.pivot = new Vector2(0.5f, 0f);
        nBtnRt.anchoredPosition = new Vector2(185f, 25f);
        nBtnRt.sizeDelta = new Vector2(320f, 58f);
        Image nImg = nBtnObj.AddComponent<Image>();
        nImg.color = new Color(0.12f, 0.55f, 0.32f);
        _endingNextBtn = nBtnObj.AddComponent<Button>();
        _endingNextBtn.onClick.AddListener(() =>
        {
            AdvanceToNextCampaign();
            _cinematicRoot.SetActive(false);
            Time.timeScale = 1f;
            GameManager.Instance?.RestartGame();
        });

        GameObject nTxtObj = new GameObject("Text");
        nTxtObj.transform.SetParent(nBtnObj.transform, false);
        RectTransform nTxtRt = nTxtObj.AddComponent<RectTransform>();
        nTxtRt.anchorMin = Vector2.zero;
        nTxtRt.anchorMax = Vector2.one;
        nTxtRt.offsetMin = Vector2.zero;
        nTxtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI nTMP = nTxtObj.AddComponent<TextMeshProUGUI>();
        nTMP.text = "SONRAKİ SEFERE GEÇ >>";
        nTMP.fontSize = 21f; // 17f -> 21f
        nTMP.fontStyle = FontStyles.Bold;
        nTMP.alignment = TextAlignmentOptions.Center;
        nTMP.color = Color.white;

        // 2. Buton: ANA MENÜYE DÖN
        GameObject mBtnObj = new GameObject("BtnMainMenu");
        mBtnObj.transform.SetParent(_endingModalObj.transform, false);
        RectTransform mBtnRt = mBtnObj.AddComponent<RectTransform>();
        mBtnRt.anchorMin = new Vector2(0.5f, 0f);
        mBtnRt.anchorMax = new Vector2(0.5f, 0f);
        mBtnRt.pivot = new Vector2(0.5f, 0f);
        mBtnRt.anchoredPosition = new Vector2(-185f, 25f);
        mBtnRt.sizeDelta = new Vector2(290f, 58f);
        Image mImg = mBtnObj.AddComponent<Image>();
        mImg.color = new Color(0.24f, 0.30f, 0.40f);
        _endingMenuBtn = mBtnObj.AddComponent<Button>();
        _endingMenuBtn.onClick.AddListener(() =>
        {
            _cinematicRoot.SetActive(false);
            Time.timeScale = 1f;
            GameManager.Instance?.GoToMainMenu();
        });

        GameObject mTxtObj = new GameObject("Text");
        mTxtObj.transform.SetParent(mBtnObj.transform, false);
        RectTransform mTxtRt = mTxtObj.AddComponent<RectTransform>();
        mTxtRt.anchorMin = Vector2.zero;
        mTxtRt.anchorMax = Vector2.one;
        mTxtRt.offsetMin = Vector2.zero;
        mTxtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI mTMP = mTxtObj.AddComponent<TextMeshProUGUI>();
        mTMP.text = "ANA MENÜYE DÖN";
        mTMP.fontSize = 21f; // 17f -> 21f
        mTMP.fontStyle = FontStyles.Bold;
        mTMP.alignment = TextAlignmentOptions.Center;
        mTMP.color = Color.white;

        _endingModalObj.SetActive(false);
    }

    public static (string speaker, Color color, string[] sentences) GetCampaignIntro(CampaignType camp)
    {
        switch (camp)
        {
            case CampaignType.ProjectChimera:
                return (
                    "[SİBER DİRENİŞ AĞI - PROJE KİMERAS]",
                    new Color(0.2f, 0.9f, 1f),
                    new string[]
                    {
                        "Global savunma yapay zekası A.P.E.X. kontrolden çıktı ve tüm otonom filoları isyana sürükledi.",
                        "Yapay zeka ağları tüm modern jetleri hackledi; geriye tek çare analog yazılımlı bu prototip savaş uçağı kaldı.",
                        "Siber virüsün ulaşamadığı tek kokpittesin... İnsanlığın son kalkanı sensin Asker!",
                        "Hava koridorlarını aş, 20 ölümcül dronu imha et ve A.P.E.X. Süper Beyin merkezini çökert!"
                    }
                );

            case CampaignType.SkyMercenary:
                return (
                    "[KORSAN İSTİHBARATI - GÖKYÜZÜ PARALI ASKERİ]",
                    new Color(1f, 0.85f, 0.2f),
                    new string[]
                    {
                        "Kanunun işlemediği tarafsız gökyüzü... Acımasız hava korsanları ve silah kartellerinin kontrolünde.",
                        "Uluslararası konsorsiyum gökleri temizlemek için tarihin en büyük av ödülünü koydu.",
                        "Milyon dolarlık kelleler ve dünyanın en tehlikeli as pilotları av sahasında seni bekliyor.",
                        "Lövyeyi kavra, irtifa kazan! 20 azılı hedefi tek tek avla ve göklerin en zengin As Pilotu ol!"
                    }
                );

            default:
                return (
                    "[HAREKAT MERKEZİ - OPERASYON KARA ŞAFAK]",
                    new Color(0.3f, 0.95f, 1f),
                    new string[]
                    {
                        "Yıl 2026... Vatan semaları gece yarısı beklenmedik bir taarruzla karartıldı.",
                        "Tüm hava üslerimiz vuruldu; geriye sadece dağ eteğinde gizlenmiş bu hangar kaldı.",
                        "Son prototip savaş uçağı ve tek bir pilot... Vatanın istikbali artık senin lövyende Asker!",
                        "Motorları çalıştır, tırmanışa geç! 20 düşman uçağını göklerden temizleyip başkenti kurtar!"
                    }
                );
        }
    }

    public static (string speaker, Color color, string[] sentences) GetBossIntro(CampaignType camp, int wave)
    {
        if (camp == CampaignType.ProjectChimera)
        {
            switch (wave)
            {
                case 5:
                    return (
                        "⚠️ [BOSS 1: A.P.E.X. SAVUNMA PROTOKOLÜ] ⚠️",
                        new Color(1f, 0.35f, 0.25f),
                        new string[]
                        {
                            "Siber Alarm! Birinci savunma duvarı aşıldı, otonom savunma protokolu doğrudan angaje oluyor!",
                            "A.P.E.X.: 'Biyolojik pilot tespit edildi. Tehdit seviyesi: Kritik. Mantıksal karar: İmha.'",
                            "Direniş: 'Yapay zeka uçuş kalıplarını hesaplıyor. Tahmin edilemez manevralarla gövdesini vur!'"
                        }
                    );
                case 10:
                    return (
                        "⚠️ [BOSS 2: A.P.E.X. SİBER HAYALET] ⚠️",
                        new Color(0.85f, 0.4f, 1f),
                        new string[]
                        {
                            "Elektronik Harp! İletişim bantları karartılıyor, sinyal bozucular tüm telemetriyi kesti!",
                            "Siber Hayalet: 'Algoritmamızda kusur yok. İnsan zihni makinelerin reflekslerine asla yetişemez.'",
                            "Direniş: 'Sinyal bozucuları aş! Çekirdek sensörlerine odaklanıp yüksek hasar ver!'"
                        }
                    );
                case 15:
                    return (
                        "⚠️ [BOSS 3: A.P.E.X. PLAZMA REAKTÖRÜ] ⚠️",
                        new Color(1f, 0.5f, 0.15f),
                        new string[]
                        {
                            "Çekirdek Uyarısı! Devasa plazma reaktörü taşıyan ağır sibernetik avcı sahaya girdi!",
                            "A.P.E.X.: 'Reaktör aşırı yüklendi. Hava koridoru atomik düzeyde buharlaştırılacak.'",
                            "Direniş: 'Reaktör tam güce ulaşmadan gövdesini delip geçmelisin, son beş dalga!'"
                        }
                    );
                default:
                    return (
                        "🚨 [FİNAL BOSS: A.P.E.X. SÜPER BEYİN MERKEZİ] 🚨",
                        new Color(1f, 0.15f, 0.15f),
                        new string[]
                        {
                            "Nihai Siber Savaş! A.P.E.X.'in ana süper beyni gökyüzünde konuşlandı!",
                            "Süper Beyin: 'İnsanlığın gökyüzündeki çağı kapandı. Yeni efendiniz yapay zeka ağıdır!'",
                            "Direniş: 'Tüm insanlığın özgürlüğü senin ellerinde! Bu son makineyi göklerden sil!'"
                        }
                    );
            }
        }
        else if (camp == CampaignType.SkyMercenary)
        {
            switch (wave)
            {
                case 5:
                    return (
                        "⚠️ [BOSS 1: DEMİR YÜREK - 500.000$ ÖDÜL] ⚠️",
                        new Color(1f, 0.45f, 0.1f),
                        new string[]
                        {
                            "Av Alarmı! Sınır korsanlarının korkulu rüyası Demir Yürek hava sahasına girdi!",
                            "Demir Yürek: 'Kafandaki yarım milyonluk ödülü almaya geldim! Hurdanı müzayedede satacağım!'",
                            "Kontraktör: 'Demir Yürek kalın zırhına güveniyor. Hızlı ol ve motorlarını delip geç!'"
                        }
                    );
                case 10:
                    return (
                        "⚠️ [BOSS 2: GÖLGE PENÇE - 1.000.000$ ÖDÜL] ⚠️",
                        new Color(0.85f, 0.4f, 1f),
                        new string[]
                        {
                            "As Pilot Tespiti! Gökyüzü kartelinin baş tetikçisi Gölge Pençe radarda belirdi!",
                            "Gölge Pençe: 'Sessizce avlar, iz bırakmam. 1 milyonluk kontratın sonu geldi çocuk!'",
                            "Kontraktör: 'Gölge Pençe fırtına gibi döner! Mermilerini boşa harcama, sabırla nişan al!'"
                        }
                    );
                case 15:
                    return (
                        "⚠️ [BOSS 3: KIRMIZI BARON - 2.500.000$ ÖDÜL] ⚠️",
                        new Color(1f, 0.25f, 0.25f),
                        new string[]
                        {
                            "Efsanevi Tehlike! Göklerin en acımasız paralı askeri Kırmızı Baron it dalaşına katıldı!",
                            "Kırmızı Baron: 'Uçuş tarzını beğendim... Ama bir ustanın kurşunlarından asla kurtulamazsın!'",
                            "Kontraktör: 'İki efsane karşı karşıya! Göklerin tek bir kralı olabilir, o da sen olacaksın!'"
                        }
                    );
                default:
                    return (
                        "🚨 [FİNAL BOSS: KARTEL BARONU VOLK - 10.000.000$ BÜYÜK ÖDÜL] 🚨",
                        new Color(1f, 0.85f, 0.15f),
                        new string[]
                        {
                            "Nihai Kontrat! Göklerin mutlak hakimi Kartel Baronu dev altın kaplama jetiyle sahada!",
                            "Baron Volk: 'Bütün filomu biçtin ama benim servetim ve ateş gücüm seni gökyüzünden silecek!'",
                            "Kontraktör: 'On milyon dolarlık büyük zafer parmaklarının ucunda! İndir onu Asker!'"
                        }
                    );
            }
        }
        else
        {
            // Kara Şafak
            switch (wave)
            {
                case 5:
                    return (
                        "⚠️ [BOSS 1: GENERAL VOLKOV - AĞIR TAARRUZ] ⚠️",
                        new Color(1f, 0.35f, 0.35f),
                        new string[]
                        {
                            "Tehlike Alarmı! Düşman öncü filosu yarıldı ancak zırhlı Taarruz Komutanı Volkov sahaya girdi!",
                            "Volkov: 'Sınırınızı tek bir jetle mi savunacaksınız? Çelik kanatlarım sizi göklerden silecek!'",
                            "Karargah: 'Volkov'un zırhı kalın ve seri mermileri ölümcül. Zayıf noktasına odaklan!'"
                        }
                    );
                case 10:
                    return (
                        "⚠️ [BOSS 2: PHANTOM - GÖRÜNMEZ GECE AVCISI] ⚠️",
                        new Color(0.2f, 0.85f, 1f),
                        new string[]
                        {
                            "Radar Uyarısı! Termal sinyalleri kaybettik, düşman hayalet interseptörle saldırıyor!",
                            "Phantom: 'Gözlerin beni göremez, mermilerim iz bırakmaz. Gökyüzündeki son nefesini ver Asker!'",
                            "Karargah: 'Phantom ani süzülüşlerle arkana sarkmaya çalışacak. Dönüş manevralarını sıklaştır!'"
                        }
                    );
                case 15:
                    return (
                        "⚠️ [BOSS 3: FIREBIRD - PLAZMA ALEV KARTALI] ⚠️",
                        new Color(1f, 0.45f, 0.1f),
                        new string[]
                        {
                            "Termal Alarm! Gökyüzünde aşırı sıcaklık yükselmesi tespit edildi! Hava koridoru alev alıyor!",
                            "Firebird: 'Cehennem ateşi başkentinizi küle çevirecek! Bizden kaçış yok, sadece yangın var!'",
                            "Karargah: 'Ateş menzilinden uzak dur! Firebird aralıksız salvo boşaltacak, salvo aralarında vur!'"
                        }
                    );
                default:
                    return (
                        "🚨 [FİNAL BOSS: GÖK HAKİMİ AMİRALİ - AMİRAL GEMİSİ] 🚨",
                        new Color(1f, 0.2f, 0.2f),
                        new string[]
                        {
                            "Nihai Alarm! Düşman ordusunun devasa Amiral Savaş Jeti doğrudan başkentin üzerine iniyor!",
                            "Amiral: 'Tüm filomu tek başına düşürdün çocuk... Ama bu amiral jeti göklerin hükümdarıdır!'",
                            "Karargah: 'Vatanın kaderi bu son it dalaşına bağlı! Bütün gücünü kullan, zafer bizim olmalı!'"
                        }
                    );
            }
        }
    }

    public static (string speaker, Color color, string[] sentences) GetCampaignEndingDialogue(CampaignType camp)
    {
        string resolution = "";
        switch (camp)
        {
            case CampaignType.ProjectChimera:
                resolution = "A.P.E.X. Süper Beyin çekirdeği çöktü! Tüm siber dron ordusu devre dışı kaldı, insanlık kurtuldu!";
                break;
            case CampaignType.SkyMercenary:
                resolution = "Kartel Baronu Volk düşürüldü! 10 Milyon dolarlık büyük ödülü aldın ve göklerin rakipsiz Ası oldun!";
                break;
            default:
                resolution = "Vatan hava sahası düşman işgalinden tamamen temizlendi! Başkent sokaklarında zafer sirenleri çalıyor!";
                break;
        }

        return (
            "[ HAREKAT ZAFERİ: EFSANEVİ AS PİLOT ]",
            new Color(1f, 0.85f, 0.15f),
            new string[]
            {
                "Büyük duman ve alev bulutları arasında son düşman amiral uçağı gök kubbeden süzülerek yere çakıldı...",
                resolution,
                "Telsizden Yükselen Ses: 'Tebrikler Efsanevi As Pilot! Adın altın harflerle göklere yazıldı!'",
                "Bir sonraki harekat seferinin gizli koordinatları hangara iletildi. Yeni zaferler seni bekliyor!"
            }
        );
    }

    #endregion

    /// <summary>
    /// Ekranda taktik askeri telsiz bandı gösterir.
    /// </summary>
    /// Ekranda taktik askeri telsiz bandı gösterir.
    /// </summary>
    public void ShowRadioTransmission(string speaker, string message, Color themeColor)
    {
        EnsureRadioHUD();

        if (_radioBannerObj == null) return;

        if (_speakerTMP != null)
        {
            _speakerTMP.text = speaker;
            _speakerTMP.color = themeColor;
        }

        if (_freqTMP != null)
        {
            _freqTMP.text = $"[FREKANS: 142.80 MHz // KRİPTO AKTİF]";
        }

        if (_messageTMP != null)
        {
            _messageTMP.text = message;
        }

        if (_transmissionCoroutine != null)
            StopCoroutine(_transmissionCoroutine);

        _transmissionCoroutine = StartCoroutine(AnimateRadioRoutine());
    }

    private System.Collections.IEnumerator AnimateRadioRoutine()
    {
        _radioBannerObj.SetActive(true);
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;

        // Telsiz bip efekti
        AudioManager.Instance?.PlayGunfire(false);

        // Hızlı Fade In (0.2 sn)
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / 0.2f);
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        // Ekranda okunabilir şekilde kalma süresi (4.5 sn)
        yield return new WaitForSecondsRealtime(4.5f);

        // Yumuşak Fade Out (0.4 sn)
        elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.4f);
            yield return null;
        }

        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        _radioBannerObj.SetActive(false);
        _transmissionCoroutine = null;
    }

    private void EnsureRadioHUD()
    {
        if (_radioBannerObj != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("RadioTransmissionHUD");
        if (existing != null)
        {
            _radioBannerObj = existing.gameObject;
        }
        else
        {
            _radioBannerObj = new GameObject("RadioTransmissionHUD");
            _radioBannerObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = _radioBannerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -65f); // Skor rozetinin hemen altında
            rt.sizeDelta = new Vector2(980f, 130f);

            Image bg = _radioBannerObj.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.05f, 0.10f, 0.98f);

            Outline outline = _radioBannerObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.85f, 1f, 0.9f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            _canvasGroup = _radioBannerObj.AddComponent<CanvasGroup>();

            // 1. Üst Satır: Konuşan Kişi & Frekans
            GameObject headerObj = new GameObject("HeaderRow");
            headerObj.transform.SetParent(_radioBannerObj.transform, false);
            RectTransform hRt = headerObj.AddComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0.65f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.offsetMin = new Vector2(20f, 0f);
            hRt.offsetMax = new Vector2(-20f, -4f);

            GameObject spkObj = new GameObject("SpeakerText");
            spkObj.transform.SetParent(headerObj.transform, false);
            RectTransform spkRt = spkObj.AddComponent<RectTransform>();
            spkRt.anchorMin = new Vector2(0f, 0f);
            spkRt.anchorMax = new Vector2(0.65f, 1f);
            spkRt.offsetMin = Vector2.zero;
            spkRt.offsetMax = Vector2.zero;

            _speakerTMP = spkObj.AddComponent<TextMeshProUGUI>();
            _speakerTMP.fontSize = 28f; // Büyütüldü
            _speakerTMP.fontStyle = FontStyles.Bold;
            _speakerTMP.alignment = TextAlignmentOptions.Left;
            _speakerTMP.extraPadding = true;
            _speakerTMP.outlineWidth = 0.22f;
            _speakerTMP.outlineColor = Color.black;

            GameObject frqObj = new GameObject("FreqText");
            frqObj.transform.SetParent(headerObj.transform, false);
            RectTransform frqRt = frqObj.AddComponent<RectTransform>();
            frqRt.anchorMin = new Vector2(0.65f, 0f);
            frqRt.anchorMax = new Vector2(1f, 1f);
            frqRt.offsetMin = Vector2.zero;
            frqRt.offsetMax = Vector2.zero;

            _freqTMP = frqObj.AddComponent<TextMeshProUGUI>();
            _freqTMP.fontSize = 22f; // Büyütüldü
            _freqTMP.fontStyle = FontStyles.Bold;
            _freqTMP.alignment = TextAlignmentOptions.Right;
            _freqTMP.color = new Color(0.5f, 0.85f, 1f, 0.9f);
            _freqTMP.extraPadding = true;

            // 2. Alt Satır: Mesaj Metni
            GameObject msgObj = new GameObject("MessageText");
            msgObj.transform.SetParent(_radioBannerObj.transform, false);
            RectTransform msgRt = msgObj.AddComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0f, 0f);
            msgRt.anchorMax = new Vector2(1f, 0.65f);
            msgRt.offsetMin = new Vector2(20f, 8f);
            msgRt.offsetMax = new Vector2(-20f, 0f);

            _messageTMP = msgObj.AddComponent<TextMeshProUGUI>();
            _messageTMP.fontSize = 28f; // Büyütüldü
            _messageTMP.fontStyle = FontStyles.Bold;
            _messageTMP.alignment = TextAlignmentOptions.Left;
            _messageTMP.color = Color.white;
            _messageTMP.lineSpacing = 5f;
            _messageTMP.extraPadding = true;
            _messageTMP.outlineWidth = 0.22f;
            _messageTMP.outlineColor = Color.black;
        }

        _canvasGroup = _radioBannerObj.GetComponent<CanvasGroup>();
        _radioBannerObj.SetActive(false);
    }
}
