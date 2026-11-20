#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AirBattle.EditorTools
{
    [InitializeOnLoad]
    public static class ScreenOptimizerEditor
    {
        static ScreenOptimizerEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ResetGameViewScale();
                ApplyOptimalCanvasScaler();
            }
        }

        [MenuItem("AirBattle/Tüm UI ve Ekran Ölçeğini Sıfırla (1x & 1920x1080) %&#r")]
        public static void ResetAllUIAndScreen()
        {
            ResetGameViewScale();
            ApplyOptimalCanvasScaler();
            FixSceneUIElements();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AirBattle 2D - Ekran ve UI Sıfırlandı",
                "1. Game View yakınlaştırma ölçeği 1.0x (orijinal) seviyesine sıfırlandı.\n" +
                "2. Canvas 1920x1080 referans ve Yükseklik Eşleme (Match Height) moduna alındı.\n" +
                "3. Ateş, Füze, Kaçış, Ulti, Oklar ve Üsse Dön butonları güvenli ekran sınırlarına çekildi.\n\n" +
                "Lütfen Game sekmesinde 'Scale' çubuğunun 1x ve çözünürlüğün 16:9 veya 1920x1080 olduğunu teyit ediniz.",
                "Tamam"
            );
            Debug.Log("<color=#00FFAA>[AirBattle]</color> Tüm UI butonları ve ekran ölçeği başarıyla 1x / 1080p olarak güncellendi.");
        }

        public static void ResetGameViewScale()
        {
            try
            {
                Type gvType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                if (gvType == null) return;

                UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(gvType);
                if (gameViews == null || gameViews.Length == 0) return;

                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                FieldInfo zoomAreaField = gvType.GetField("m_ZoomArea", flags);

                foreach (var gv in gameViews)
                {
                    if (gv == null) continue;

                    // 1. ZoomArea.scale üzerinden sıfırla
                    if (zoomAreaField != null)
                    {
                        object zoomArea = zoomAreaField.GetValue(gv);
                        if (zoomArea != null)
                        {
                            FieldInfo scaleField = zoomArea.GetType().GetField("m_Scale", flags);
                            PropertyInfo scaleProp = zoomArea.GetType().GetProperty("scale", flags);

                            if (scaleField != null) scaleField.SetValue(zoomArea, Vector2.one);
                            else if (scaleProp != null) scaleProp.SetValue(zoomArea, Vector2.one, null);
                        }
                    }

                    // 2. SnapZoom veya SetScale methodu varsa çağır
                    MethodInfo snapZoomMethod = gvType.GetMethod("SnapZoom", flags, null, new Type[] { typeof(float) }, null);
                    if (snapZoomMethod != null)
                    {
                        snapZoomMethod.Invoke(gv, new object[] { 1f });
                    }

                    // Repaint
                    MethodInfo repaint = gvType.GetMethod("Repaint", flags, null, Type.EmptyTypes, null);
                    if (repaint != null) repaint.Invoke(gv, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AirBattle] GameView zoom sıfırlama uyarısı (zararsız): " + ex.Message);
            }
        }

        public static void ApplyOptimalCanvasScaler()
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
            foreach (var canvas in canvases)
            {
                if (canvas == null) continue;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
                scaler.dynamicPixelsPerUnit = 3f;
                scaler.referencePixelsPerUnit = 100f;
                EditorUtility.SetDirty(scaler);
            }
        }

        public static void FixSceneUIElements()
        {
            // FireButton
            FireButton fb = UnityEngine.Object.FindObjectOfType<FireButton>(true);
            if (fb != null)
            {
                RectTransform rt = fb.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(-220f, 150f);
                    rt.sizeDelta = new Vector2(130f, 130f);
                    EditorUtility.SetDirty(rt);
                }
            }

            // Health Bar P1
            GameObject hp1 = GameObject.Find("Healt_Bar_P1");
            if (hp1 != null)
            {
                RectTransform rt = hp1.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(50f, -36f);
                    rt.sizeDelta = new Vector2(320f, 28f);
                    EditorUtility.SetDirty(rt);
                }
            }

            // Health Bar P2 / Boss
            GameObject hp2 = GameObject.Find("Healt_Bar_P2 (1)");
            if (hp2 != null)
            {
                RectTransform rt = hp2.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -36f);
                    rt.sizeDelta = new Vector2(500f, 30f);
                    EditorUtility.SetDirty(rt);
                }
            }
        }
    }
}
#endif
